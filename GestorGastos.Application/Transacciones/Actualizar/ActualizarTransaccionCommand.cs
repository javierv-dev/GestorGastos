using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Actualizar;

// Devuelve false cuando la transacción no existe.
public record ActualizarTransaccionCommand(
    Guid Id,
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion? Categoria,
    DateTime Fecha
) : IRequest<bool>;

public class ActualizarTransaccionHandler(ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<ActualizarTransaccionCommand, bool>
{
    public async Task<bool> Handle(ActualizarTransaccionCommand command, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (transaccion is null)
            return false;

        transaccion.Actualizar(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }
}
