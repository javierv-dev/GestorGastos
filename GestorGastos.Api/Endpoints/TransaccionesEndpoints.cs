using GestorGastos.Infrastructure.Persistence;
using GestorGastos.Api.Dtos;
using GestorGastos.Domain;
using GestorGastos.Domain.Transacciones;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Api.Endpoints;

public static class TransaccionesEndpoints
{
    private const decimal UmbralSaldoBajo = 100m;

    public static void MapTransaccionesEndpoints(this WebApplication app)
    {
        var transacciones = app.MapGroup("/transacciones");

        transacciones.MapPost("/", CrearTransaccion);
        transacciones.MapGet("/", ListarTransacciones);
        transacciones.MapGet("/saldo", ObtenerSaldo);
        transacciones.MapGet("/resumen", ObtenerResumen);
        transacciones.MapGet("/{id:guid}", ObtenerPorId);
        transacciones.MapPut("/{id:guid}", ActualizarTransaccion);
        transacciones.MapDelete("/{id:guid}", EliminarTransaccion);
    }

    private static async Task<IResult> CrearTransaccion(CrearTransaccionRequest request, GestorGastosDbContext db)
    {
        Transaccion transaccion;
        try
        {
            transaccion = Transaccion.Crear(
                request.Descripcion, request.Monto, request.Tipo, request.Categoria, request.Fecha);
        }
        catch (DomainException ex)
        {
            return ProblemaDeDominio(ex);
        }

        db.Transacciones.Add(transaccion);
        await db.SaveChangesAsync();

        var saldoActual = await db.Transacciones
            .SumAsync(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto);

        var saldoBajo = transaccion switch
        {
            { Tipo: TipoTransaccion.Egreso } when saldoActual < UmbralSaldoBajo => true,
            _ => false
        };

        return Results.Created(
            $"/transacciones/{transaccion.Id}",
            TransaccionResponse.Desde(transaccion, saldoBajo));
    }

    private static async Task<IResult> ListarTransacciones(
        DateTime? desde, DateTime? hasta, CategoriaTransaccion? categoria, GestorGastosDbContext db)
    {
        var query = db.Transacciones.AsQueryable();

        if (desde is not null) query = query.Where(t => t.Fecha >= desde.Value.Date);
        if (hasta is not null) query = query.Where(t => t.Fecha < hasta.Value.Date.AddDays(1));
        if (categoria is not null) query = query.Where(t => t.Categoria == categoria);

        var transacciones = await query.ToListAsync();
        return Results.Ok(transacciones.Select(t => TransaccionResponse.Desde(t)));
    }

    private static async Task<IResult> ObtenerSaldo(GestorGastosDbContext db)
    {
        var saldo = await db.Transacciones
            .SumAsync(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto);
        return Results.Ok(new { saldo });
    }

    private static async Task<IResult> ObtenerResumen(GestorGastosDbContext db)
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
    }

    private static async Task<IResult> ObtenerPorId(Guid id, GestorGastosDbContext db) =>
        await db.Transacciones.FindAsync(id) is { } transaccion
            ? Results.Ok(TransaccionResponse.Desde(transaccion))
            : Results.NotFound();

    private static async Task<IResult> ActualizarTransaccion(
        Guid id, CrearTransaccionRequest request, GestorGastosDbContext db)
    {
        var transaccion = await db.Transacciones.FindAsync(id);
        if (transaccion is null) return Results.NotFound();

        try
        {
            transaccion.Actualizar(
                request.Descripcion, request.Monto, request.Tipo, request.Categoria, request.Fecha);
        }
        catch (DomainException ex)
        {
            return ProblemaDeDominio(ex);
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> EliminarTransaccion(Guid id, GestorGastosDbContext db)
    {
        var transaccion = await db.Transacciones.FindAsync(id);
        if (transaccion is null) return Results.NotFound();

        db.Transacciones.Remove(transaccion);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static IResult ProblemaDeDominio(DomainException ex) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Campo] = [ex.Message] });
}
