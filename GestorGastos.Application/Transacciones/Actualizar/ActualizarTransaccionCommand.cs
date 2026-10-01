using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Presupuestos;
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

public class ActualizarTransaccionHandler(ITransaccionRepository repositorio, ComprobadorDePresupuesto comprobador, IUnitOfWork unitOfWork)
    : IRequestHandler<ActualizarTransaccionCommand, Result>
{
    public async Task<Result> Handle(ActualizarTransaccionCommand command, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (transaccion is null)
            return Result.Failure(TransaccionErrors.NoEncontrada);

        // Se valida y se comprueba el presupuesto con una transacción candidata ANTES de tocar la real:
        // si algo falla, la entidad que EF está siguiendo no queda modificada a medias.
        var candidata = Transaccion.Crear(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);
        if (candidata.IsFailure)
            return candidata;

        // La transacción actual se excluye de la suma del mes: ya está contada con su valor anterior.
        var presupuesto = await comprobador.ComprobarAsync(candidata.Value, command.Id, cancellationToken);
        if (presupuesto.IsFailure)
            return presupuesto;

        var actualizada = transaccion.Actualizar(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);
        if (actualizada.IsFailure)
            return actualizada;

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
