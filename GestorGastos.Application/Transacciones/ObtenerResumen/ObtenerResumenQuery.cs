using MediatR;

namespace GestorGastos.Application.Transacciones.ObtenerResumen;

public record ObtenerResumenQuery : IRequest<IReadOnlyList<ResumenCategoria>>;

public class ObtenerResumenHandler(ITransaccionRepository repositorio)
    : IRequestHandler<ObtenerResumenQuery, IReadOnlyList<ResumenCategoria>>
{
    public Task<IReadOnlyList<ResumenCategoria>> Handle(
        ObtenerResumenQuery query, CancellationToken cancellationToken) =>
        repositorio.ObtenerResumenPorCategoriaAsync(cancellationToken);
}
