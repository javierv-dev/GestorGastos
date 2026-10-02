using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Presupuestos.Crear;

public record CrearPresupuestoCommand(CategoriaTransaccion Categoria, decimal LimiteMensual) : IRequest<Result<PresupuestoDto>>;

public class CrearPresupuestoHandler(IPresupuestoRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<CrearPresupuestoCommand, Result<PresupuestoDto>>
{
    public async Task<Result<PresupuestoDto>> Handle(CrearPresupuestoCommand command, CancellationToken cancellationToken)
    {
        var creado = Presupuesto.Crear(command.Categoria, command.LimiteMensual);
        if (creado.IsFailure)
            return Result.Failure<PresupuestoDto>(creado.Error);

        // Regla que el dominio no puede saber solo: necesita mirar los presupuestos existentes.
        if (await repositorio.ObtenerPorCategoriaAsync(command.Categoria, cancellationToken) is not null)
            return Result.Failure<PresupuestoDto>(PresupuestoErrors.YaExiste);

        repositorio.Agregar(creado.Value);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        return Result.Success(PresupuestoDto.Desde(creado.Value));
    }
}
