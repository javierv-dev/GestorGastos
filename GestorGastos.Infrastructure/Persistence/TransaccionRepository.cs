using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Transacciones;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Infrastructure.Persistence;

public class TransaccionRepository(GestorGastosDbContext db) : ITransaccionRepository
{
    public async Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Transacciones.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Transaccion>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.Transacciones.AsQueryable();

        if (desde is not null)
            query = query.Where(t => t.Fecha >= desde.Value.Date);
        if (hasta is not null)
            query = query.Where(t => t.Fecha < hasta.Value.Date.AddDays(1));
        if (categoria is not null)
            query = query.Where(t => t.Categoria == categoria);

        return await query.ToListAsync(cancellationToken);
    }

    public Task<decimal> ObtenerSaldoAsync(CancellationToken cancellationToken = default) =>
        db.Transacciones.SumAsync(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto, cancellationToken);

    public async Task<IReadOnlyList<ResumenCategoria>> ObtenerResumenPorCategoriaAsync(CancellationToken cancellationToken = default) =>
        await db
            .Transacciones.GroupBy(t => t.Categoria)
            .OrderBy(g => g.Key)
            .Select(g => new ResumenCategoria(
                g.Key,
                g.Sum(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : 0),
                g.Sum(t => t.Tipo == TipoTransaccion.Egreso ? t.Monto : 0)
            ))
            .ToListAsync(cancellationToken);

    public Task<decimal> ObtenerEgresosDelMesAsync(
        CategoriaTransaccion categoria,
        DateTime fecha,
        Guid? excluirTransaccionId = null,
        CancellationToken cancellationToken = default
    )
    {
        var inicioDelMes = new DateTime(fecha.Year, fecha.Month, 1);
        var inicioDelMesSiguiente = inicioDelMes.AddMonths(1);

        var egresos = db.Transacciones.Where(t =>
            t.Tipo == TipoTransaccion.Egreso && t.Categoria == categoria && t.Fecha >= inicioDelMes && t.Fecha < inicioDelMesSiguiente
        );

        if (excluirTransaccionId is { } id)
            egresos = egresos.Where(t => t.Id != id);

        return egresos.SumAsync(t => t.Monto, cancellationToken);
    }

    public void Agregar(Transaccion transaccion) => db.Transacciones.Add(transaccion);

    public void Eliminar(Transaccion transaccion) => db.Transacciones.Remove(transaccion);
}
