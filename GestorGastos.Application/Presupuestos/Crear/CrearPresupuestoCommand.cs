using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Presupuestos.Crear;

public record CrearPresupuestoCommand(CategoriaTransaccion Categoria, decimal LimiteMensual) : IRequest<Result<Presupuesto>>;

public class CrearPresupuestoHandler(IPresupuestoRepository repositorio, IPresupuestoConsultas consultas, IUnitOfWork unitOfWork)
    : IRequestHandler<CrearPresupuestoCommand, Result<Presupuesto>>
{
    public async Task<Result<Presupuesto>> Handle(CrearPresupuestoCommand command, CancellationToken cancellationToken)
    {
        var creado = Presupuesto.Crear(command.Categoria, command.LimiteMensual);
        if (creado.IsFailure)
            return creado;

        // Regla que el dominio no puede saber solo: necesita mirar los presupuestos existentes.
        if (await consultas.ObtenerPorCategoriaAsync(command.Categoria, cancellationToken) is not null)
            return Result.Failure<Presupuesto>(PresupuestoErrors.YaExiste);

        repositorio.Agregar(creado.Value);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        return creado;
    }
}
