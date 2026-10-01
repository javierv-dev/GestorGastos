using GestorGastos.Api.Dtos;
using GestorGastos.Api.Extensions;
using GestorGastos.Application.Presupuestos.ActualizarLimite;
using GestorGastos.Application.Presupuestos.Crear;
using GestorGastos.Application.Presupuestos.Eliminar;
using GestorGastos.Application.Presupuestos.Listar;
using GestorGastos.Application.Presupuestos.ObtenerPorId;
using MediatR;

namespace GestorGastos.Api.Endpoints;

public static class PresupuestosEndpoints
{
    public static void MapPresupuestosEndpoints(this WebApplication app)
    {
        var presupuestos = app.MapGroup("/presupuestos");

        presupuestos.MapPost("/", CrearPresupuesto);
        presupuestos.MapGet("/", ListarPresupuestos);
        presupuestos.MapGet("/{id:guid}", ObtenerPorId);
        presupuestos.MapPut("/{id:guid}", ActualizarLimite);
        presupuestos.MapDelete("/{id:guid}", EliminarPresupuesto);
    }

    private static async Task<IResult> CrearPresupuesto(CrearPresupuestoRequest request, ISender sender)
    {
        var resultado = await sender.Send(new CrearPresupuestoCommand(request.Categoria, request.LimiteMensual));

        return resultado.Match(presupuesto => Results.Created($"/presupuestos/{presupuesto.Id}", PresupuestoResponse.Desde(presupuesto)));
    }

    private static async Task<IResult> ListarPresupuestos(ISender sender)
    {
        var presupuestos = await sender.Send(new ListarPresupuestosQuery());
        return Results.Ok(presupuestos.Select(PresupuestoResponse.Desde));
    }

    private static async Task<IResult> ObtenerPorId(Guid id, ISender sender)
    {
        var resultado = await sender.Send(new ObtenerPresupuestoPorIdQuery(id));
        return resultado.Match(presupuesto => Results.Ok(PresupuestoResponse.Desde(presupuesto)));
    }

    private static async Task<IResult> ActualizarLimite(Guid id, ActualizarPresupuestoRequest request, ISender sender)
    {
        var resultado = await sender.Send(new ActualizarLimitePresupuestoCommand(id, request.LimiteMensual));
        return resultado.Match(Results.NoContent);
    }

    private static async Task<IResult> EliminarPresupuesto(Guid id, ISender sender)
    {
        var resultado = await sender.Send(new EliminarPresupuestoCommand(id));
        return resultado.Match(Results.NoContent);
    }
}
