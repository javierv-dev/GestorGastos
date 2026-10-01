using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Presupuestos;

public interface IPresupuestoRepository
{
    Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Presupuesto>> ListarAsync(CancellationToken cancellationToken = default);

    void Agregar(Presupuesto presupuesto);

    void Eliminar(Presupuesto presupuesto);
}
