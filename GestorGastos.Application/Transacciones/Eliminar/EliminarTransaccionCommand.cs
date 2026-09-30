using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Eliminar;

public record EliminarTransaccionCommand(Guid Id) : IRequest<Result>;

public class EliminarTransaccionHandler(ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<EliminarTransaccionCommand, Result>
{
    public async Task<Result> Handle(EliminarTransaccionCommand command, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (transaccion is null)
            return Result.Failure(TransaccionErrors.NoEncontrada);

        repositorio.Eliminar(transaccion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
