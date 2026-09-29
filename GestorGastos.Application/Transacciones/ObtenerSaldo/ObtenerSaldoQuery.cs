using MediatR;

namespace GestorGastos.Application.Transacciones.ObtenerSaldo;

public record ObtenerSaldoQuery : IRequest<decimal>;

public class ObtenerSaldoHandler(ITransaccionRepository repositorio) : IRequestHandler<ObtenerSaldoQuery, decimal>
{
    public Task<decimal> Handle(ObtenerSaldoQuery query, CancellationToken cancellationToken) =>
        repositorio.ObtenerSaldoAsync(cancellationToken);
}
