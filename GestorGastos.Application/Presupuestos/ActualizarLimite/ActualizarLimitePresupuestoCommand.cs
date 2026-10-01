using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using MediatR;

namespace GestorGastos.Application.Presupuestos.ActualizarLimite;

public record ActualizarLimitePresupuestoCommand(Guid Id, decimal LimiteMensual) : IRequest<Result>;

public class ActualizarLimitePresupuestoHandler(IPresupuestoRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<ActualizarLimitePresupuestoCommand, Result>
{
    public async Task<Result> Handle(ActualizarLimitePresupuestoCommand command, CancellationToken cancellationToken)
    {
        var presupuesto = await repositorio.ObtenerPorIdAsync(command.Id, cancellationToken);
        if (presupuesto is null)
            return Result.Failure(PresupuestoErrors.NoEncontrado);

        var cambio = presupuesto.CambiarLimite(command.LimiteMensual);
        if (cambio.IsFailure)
            return cambio;

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
