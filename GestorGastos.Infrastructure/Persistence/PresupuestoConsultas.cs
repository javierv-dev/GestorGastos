using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Infrastructure.Persistence;

public class PresupuestoConsultas(GestorGastosDbContext db) : IPresupuestoConsultas
{
    public async Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Presupuestos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default) =>
        db.Presupuestos.AsNoTracking().FirstOrDefaultAsync(p => p.Categoria == categoria, cancellationToken);

    public async Task<IReadOnlyList<Presupuesto>> ListarAsync(CancellationToken cancellationToken = default) =>
        await db.Presupuestos.AsNoTracking().OrderBy(p => p.Categoria).ToListAsync(cancellationToken);
}
