using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using MediatR;

namespace GestorGastos.Application.Presupuestos.ObtenerPorId;

public record ObtenerPresupuestoPorIdQuery(Guid Id) : IRequest<Result<Presupuesto>>;

public class ObtenerPresupuestoPorIdHandler(IPresupuestoConsultas repositorio)
    : IRequestHandler<ObtenerPresupuestoPorIdQuery, Result<Presupuesto>>
{
    public async Task<Result<Presupuesto>> Handle(ObtenerPresupuestoPorIdQuery query, CancellationToken cancellationToken)
    {
        var presupuesto = await repositorio.ObtenerPorIdAsync(query.Id, cancellationToken);

        return presupuesto is null ? Result.Failure<Presupuesto>(PresupuestoErrors.NoEncontrado) : Result.Success(presupuesto);
    }
}
