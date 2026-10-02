using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using MediatR;

namespace GestorGastos.Application.Presupuestos.ObtenerPorId;

public record ObtenerPresupuestoPorIdQuery(Guid Id) : IRequest<Result<PresupuestoDto>>;

public class ObtenerPresupuestoPorIdHandler(IPresupuestoConsultas repositorio)
    : IRequestHandler<ObtenerPresupuestoPorIdQuery, Result<PresupuestoDto>>
{
    public async Task<Result<PresupuestoDto>> Handle(ObtenerPresupuestoPorIdQuery query, CancellationToken cancellationToken)
    {
        var presupuesto = await repositorio.ObtenerPorIdAsync(query.Id, cancellationToken);

        return presupuesto is null ? Result.Failure<PresupuestoDto>(PresupuestoErrors.NoEncontrado) : Result.Success(presupuesto);
    }
}
