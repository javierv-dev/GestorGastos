using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.ObtenerPorId;

public record ObtenerTransaccionPorIdQuery(Guid Id) : IRequest<Result<TransaccionDto>>;

public class ObtenerTransaccionPorIdHandler(ITransaccionConsultas repositorio)
    : IRequestHandler<ObtenerTransaccionPorIdQuery, Result<TransaccionDto>>
{
    public async Task<Result<TransaccionDto>> Handle(ObtenerTransaccionPorIdQuery query, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(query.Id, cancellationToken);

        return transaccion is null ? Result.Failure<TransaccionDto>(TransaccionErrors.NoEncontrada) : Result.Success(transaccion);
    }
}
