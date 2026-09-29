using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.ObtenerPorId;

public record ObtenerTransaccionPorIdQuery(Guid Id) : IRequest<Transaccion?>;

public class ObtenerTransaccionPorIdHandler(ITransaccionRepository repositorio)
    : IRequestHandler<ObtenerTransaccionPorIdQuery, Transaccion?>
{
    public Task<Transaccion?> Handle(ObtenerTransaccionPorIdQuery query, CancellationToken cancellationToken) =>
        repositorio.ObtenerPorIdAsync(query.Id, cancellationToken);
}
