using GestorGastos.Domain.Presupuestos;
using MediatR;

namespace GestorGastos.Application.Presupuestos.Listar;

public record ListarPresupuestosQuery : IRequest<IReadOnlyList<Presupuesto>>;

public class ListarPresupuestosHandler(IPresupuestoRepository repositorio)
    : IRequestHandler<ListarPresupuestosQuery, IReadOnlyList<Presupuesto>>
{
    public Task<IReadOnlyList<Presupuesto>> Handle(ListarPresupuestosQuery query, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(cancellationToken);
}
