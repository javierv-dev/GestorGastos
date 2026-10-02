using GestorGastos.Domain.Presupuestos;

namespace GestorGastos.Application.Presupuestos;

// Rol de escritura: persistencia del agregado Presupuesto (cargar con seguimiento, agregar, eliminar).
public interface IPresupuestoRepository
{
    Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Agregar(Presupuesto presupuesto);

    void Eliminar(Presupuesto presupuesto);
}
