using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Actualizar;

public record ActualizarTransaccionCommand(
    Guid Id,
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion? Categoria,
    DateTime Fecha
) : IRequest<Result>;

public class ActualizarTransaccionHandler(ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<ActualizarTransaccionCommand, Result>
{
    public async Task<Result> Handle(ActualizarTransaccionCommand command, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (transaccion is null)
            return Result.Failure(TransaccionErrors.NoEncontrada);

        var actualizada = transaccion.Actualizar(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);
        if (actualizada.IsFailure)
            return actualizada;

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
