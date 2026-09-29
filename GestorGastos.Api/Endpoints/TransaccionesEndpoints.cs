using GestorGastos.Api.Dtos;
using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Transacciones;
using GestorGastos.Domain;
using GestorGastos.Domain.Transacciones;

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

    private static async Task<IResult> CrearTransaccion(
        CrearTransaccionRequest request, ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
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

        repositorio.Agregar(transaccion);
        await unitOfWork.GuardarCambiosAsync();

        var saldoActual = await repositorio.ObtenerSaldoAsync();

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
        DateTime? desde, DateTime? hasta, CategoriaTransaccion? categoria, ITransaccionRepository repositorio)
    {
        var transacciones = await repositorio.ListarAsync(desde, hasta, categoria);
        return Results.Ok(transacciones.Select(t => TransaccionResponse.Desde(t)));
    }

    private static async Task<IResult> ObtenerSaldo(ITransaccionRepository repositorio)
    {
        var saldo = await repositorio.ObtenerSaldoAsync();
        return Results.Ok(new { saldo });
    }

    private static async Task<IResult> ObtenerResumen(ITransaccionRepository repositorio) =>
        Results.Ok(await repositorio.ObtenerResumenPorCategoriaAsync());

    private static async Task<IResult> ObtenerPorId(Guid id, ITransaccionRepository repositorio) =>
        await repositorio.ObtenerPorIdAsync(id) is { } transaccion
            ? Results.Ok(TransaccionResponse.Desde(transaccion))
            : Results.NotFound();

    private static async Task<IResult> ActualizarTransaccion(
        Guid id, CrearTransaccionRequest request, ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(id);
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

        await unitOfWork.GuardarCambiosAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> EliminarTransaccion(
        Guid id, ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(id);
        if (transaccion is null) return Results.NotFound();

        repositorio.Eliminar(transaccion);
        await unitOfWork.GuardarCambiosAsync();
        return Results.NoContent();
    }

    private static IResult ProblemaDeDominio(DomainException ex) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Campo] = [ex.Message] });
}
