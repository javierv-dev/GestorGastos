using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Presupuestos;

// Rol de lectura: consultas de solo lectura que no modifican el estado ni lo siguen.
public interface IPresupuestoConsultas
{
    Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Presupuesto>> ListarAsync(CancellationToken cancellationToken = default);
}
