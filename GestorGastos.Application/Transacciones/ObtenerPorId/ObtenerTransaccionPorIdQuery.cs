using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.ObtenerPorId;

public record ObtenerTransaccionPorIdQuery(Guid Id) : IRequest<Result<Transaccion>>;

public class ObtenerTransaccionPorIdHandler(ITransaccionRepository repositorio)
    : IRequestHandler<ObtenerTransaccionPorIdQuery, Result<Transaccion>>
{
    public async Task<Result<Transaccion>> Handle(ObtenerTransaccionPorIdQuery query, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(query.Id, cancellationToken);

        return transaccion is null ? Result.Failure<Transaccion>(TransaccionErrors.NoEncontrada) : Result.Success(transaccion);
    }
}
