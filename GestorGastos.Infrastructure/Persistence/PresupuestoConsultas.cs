using GestorGastos.Application.Presupuestos;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Infrastructure.Persistence;

// Proyecta directamente a DTO: la base devuelve solo esas columnas y EF no sigue ninguna entidad.
public class PresupuestoConsultas(GestorGastosDbContext db) : IPresupuestoConsultas
{
    public async Task<PresupuestoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Presupuestos.Where(p => p.Id == id).Select(PresupuestoDto.Proyeccion).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PresupuestoDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        await db.Presupuestos.OrderBy(p => p.Categoria).Select(PresupuestoDto.Proyeccion).ToListAsync(cancellationToken);
}
