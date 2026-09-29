using GestorGastos.Application.Abstractions;
using MediatR;

namespace GestorGastos.Application.Transacciones.Eliminar;

// Devuelve false cuando la transacción no existe.
public record EliminarTransaccionCommand(Guid Id) : IRequest<bool>;

public class EliminarTransaccionHandler(ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<EliminarTransaccionCommand, bool>
{
    public async Task<bool> Handle(EliminarTransaccionCommand command, CancellationToken cancellationToken)
    {
        var transaccion = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (transaccion is null) return false;

        repositorio.Eliminar(transaccion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }
}
