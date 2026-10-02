using MediatR;

namespace GestorGastos.Application.Presupuestos.Listar;

public record ListarPresupuestosQuery : IRequest<IReadOnlyList<PresupuestoDto>>;

public class ListarPresupuestosHandler(IPresupuestoConsultas repositorio)
    : IRequestHandler<ListarPresupuestosQuery, IReadOnlyList<PresupuestoDto>>
{
    public Task<IReadOnlyList<PresupuestoDto>> Handle(ListarPresupuestosQuery query, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(cancellationToken);
}
