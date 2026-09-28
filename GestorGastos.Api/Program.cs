using System.Text.Json.Serialization;
using GestorGastos.Api.Data;
using GestorGastos.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<GestorGastosDbContext>(opt => opt.UseSqlite("Data Source=gastos.db"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "GestorGastos.Api v1");
    });
}

app.UseHttpsRedirection();

var transacciones = app.MapGroup("/transacciones");

const decimal UmbralSaldoBajo = 100m;

static IResult? ValidarMonto(decimal monto) =>
    monto <= 0
        ? Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Monto"] = ["El monto debe ser mayor a cero."]
        })
        : null;

transacciones.MapPost("/", async (Transaccion transaccion, GestorGastosDbContext db) =>
{
    if (ValidarMonto(transaccion.Monto) is { } error) return error;

    transaccion.Id = Guid.NewGuid();
    db.Transacciones.Add(transaccion);
    await db.SaveChangesAsync();

    var saldoActual = await db.Transacciones
        .SumAsync(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto);

    var saldoBajo = transaccion switch
    {
        { Tipo: TipoTransaccion.Egreso } when saldoActual < UmbralSaldoBajo => true,
        _ => false
    };

    return Results.Created($"/transacciones/{transaccion.Id}", new
    {
        transaccion.Id,
        transaccion.Descripcion,
        transaccion.Monto,
        transaccion.Tipo,
        transaccion.Categoria,
        transaccion.Fecha,
        saldoBajo
    });
});

transacciones.MapGet("/", async (
    DateTime? desde,
    DateTime? hasta,
    CategoriaTransaccion? categoria,
    GestorGastosDbContext db) =>
{
    var query = db.Transacciones.AsQueryable();

    if (desde is not null) query = query.Where(t => t.Fecha >= desde.Value.Date);
    if (hasta is not null) query = query.Where(t => t.Fecha < hasta.Value.Date.AddDays(1));
    if (categoria is not null) query = query.Where(t => t.Categoria == categoria);

    return await query.ToListAsync();
});

transacciones.MapGet("/saldo", async (GestorGastosDbContext db) =>
{
    var saldo = await db.Transacciones
        .SumAsync(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto);
    return Results.Ok(new { saldo });
});

transacciones.MapGet("/resumen", async (GestorGastosDbContext db) =>
{
    var resumen = await db.Transacciones
        .GroupBy(t => t.Categoria)
        .Select(g => new
        {
            categoria = g.Key,
            totalIngresos = g.Sum(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : 0),
            totalEgresos = g.Sum(t => t.Tipo == TipoTransaccion.Egreso ? t.Monto : 0)
        })
        .OrderBy(r => r.categoria)
        .ToListAsync();
    return Results.Ok(resumen);
});

transacciones.MapGet("/{id:guid}", async (Guid id, GestorGastosDbContext db) =>
    await db.Transacciones.FindAsync(id) is { } transaccion
        ? Results.Ok(transaccion)
        : Results.NotFound());

transacciones.MapPut("/{id:guid}", async (Guid id, Transaccion input, GestorGastosDbContext db) =>
{
    if (ValidarMonto(input.Monto) is { } error) return error;

    var transaccion = await db.Transacciones.FindAsync(id);
    if (transaccion is null) return Results.NotFound();

    transaccion.Descripcion = input.Descripcion;
    transaccion.Monto = input.Monto;
    transaccion.Tipo = input.Tipo;
    transaccion.Categoria = input.Categoria;
    transaccion.Fecha = input.Fecha;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

transacciones.MapDelete("/{id:guid}", async (Guid id, GestorGastosDbContext db) =>
{
    var transaccion = await db.Transacciones.FindAsync(id);
    if (transaccion is null) return Results.NotFound();

    db.Transacciones.Remove(transaccion);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
