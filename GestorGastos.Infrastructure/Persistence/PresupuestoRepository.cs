using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Presupuestos;

namespace GestorGastos.Infrastructure.Persistence;

public class PresupuestoRepository(GestorGastosDbContext db) : IPresupuestoRepository
{
    public async Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Presupuestos.FindAsync([id], cancellationToken);

    public void Agregar(Presupuesto presupuesto) => db.Presupuestos.Add(presupuesto);

    public void Eliminar(Presupuesto presupuesto) => db.Presupuestos.Remove(presupuesto);
}
