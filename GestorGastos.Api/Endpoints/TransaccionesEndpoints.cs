using GestorGastos.Api.Dtos;
using GestorGastos.Api.Extensions;
using GestorGastos.Application.Transacciones.Actualizar;
using GestorGastos.Application.Transacciones.Crear;
using GestorGastos.Application.Transacciones.Eliminar;
using GestorGastos.Application.Transacciones.Listar;
using GestorGastos.Application.Transacciones.ObtenerPorId;
using GestorGastos.Application.Transacciones.ObtenerResumen;
using GestorGastos.Application.Transacciones.ObtenerSaldo;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Api.Endpoints;

public static class TransaccionesEndpoints
{
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
        CrearTransaccionRequest request,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var resultado = await sender.Send(
            new CrearTransaccionCommand(request.Descripcion, request.Monto, request.Tipo, request.Categoria, request.Fecha),
            cancellationToken
        );

        return resultado.Match(creada =>
            Results.Created($"/transacciones/{creada.Transaccion.Id}", TransaccionResponse.Desde(creada.Transaccion, creada.SaldoBajo))
        );
    }

    private static async Task<IResult> ListarTransacciones(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var transacciones = await sender.Send(new ListarTransaccionesQuery(desde, hasta, categoria), cancellationToken);
        return Results.Ok(transacciones.Select(t => TransaccionResponse.Desde(t)));
    }

    private static async Task<IResult> ObtenerSaldo(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(new { saldo = await sender.Send(new ObtenerSaldoQuery(), cancellationToken) });

    private static async Task<IResult> ObtenerResumen(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new ObtenerResumenQuery(), cancellationToken));

    private static async Task<IResult> ObtenerPorId(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObtenerTransaccionPorIdQuery(id), cancellationToken);
        return resultado.Match(transaccion => Results.Ok(TransaccionResponse.Desde(transaccion)));
    }

    private static async Task<IResult> ActualizarTransaccion(
        Guid id,
        CrearTransaccionRequest request,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var resultado = await sender.Send(
            new ActualizarTransaccionCommand(id, request.Descripcion, request.Monto, request.Tipo, request.Categoria, request.Fecha),
            cancellationToken
        );

        return resultado.Match(Results.NoContent);
    }

    private static async Task<IResult> EliminarTransaccion(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new EliminarTransaccionCommand(id), cancellationToken);
        return resultado.Match(Results.NoContent);
    }
}
