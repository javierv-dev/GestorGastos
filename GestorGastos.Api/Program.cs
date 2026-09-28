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
    return Results.Created($"/transacciones/{transaccion.Id}", transaccion);
});

transacciones.MapGet("/", async (GestorGastosDbContext db) =>
    await db.Transacciones.ToListAsync());

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
