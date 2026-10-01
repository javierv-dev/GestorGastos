using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using MediatR;

namespace GestorGastos.Application.Presupuestos.Eliminar;

public record EliminarPresupuestoCommand(Guid Id) : IRequest<Result>;

public class EliminarPresupuestoHandler(IPresupuestoRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<EliminarPresupuestoCommand, Result>
{
    public async Task<Result> Handle(EliminarPresupuestoCommand command, CancellationToken cancellationToken)
    {
        var presupuesto = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (presupuesto is null)
            return Result.Failure(PresupuestoErrors.NoEncontrado);

        repositorio.Eliminar(presupuesto);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
