using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Presupuestos;

// Rol de escritura: persistencia del agregado Presupuesto (cargarlo con seguimiento para decidir o modificarlo, agregar, eliminar).
public interface IPresupuestoRepository
{
    Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default);

    void Agregar(Presupuesto presupuesto);

    void Eliminar(Presupuesto presupuesto);
}
