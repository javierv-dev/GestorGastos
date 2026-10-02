using GestorGastos.Application.Common;
using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Transacciones;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Infrastructure.Persistence;

public class TransaccionConsultas(GestorGastosDbContext db) : ITransaccionConsultas
{
    public async Task<TransaccionDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Transacciones.Where(t => t.Id == id).Select(TransaccionDto.Proyeccion).FirstOrDefaultAsync(cancellationToken);

    public async Task<ResultadoPaginado<TransaccionDto>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default
    )
    {
        // Sin AsNoTracking: al proyectar a DTO, EF no sigue ninguna entidad.
        IQueryable<Transaccion> query = db.Transacciones;

        if (desde is not null)
            query = query.Where(t => t.Fecha >= desde.Value.Date);
        if (hasta is not null)
            query = query.Where(t => t.Fecha < hasta.Value.Date.AddDays(1));
        if (categoria is not null)
            query = query.Where(t => t.Categoria == categoria);

        var total = await query.CountAsync(cancellationToken);

        // En long para que una página enorme no desborde; si ya se pasó del final, no hace falta una segunda consulta.
        var saltar = (long)(pagina - 1) * tamanoPagina;
        if (saltar >= total)
            return new ResultadoPaginado<TransaccionDto>([], pagina, tamanoPagina, total);

        // Sin un orden total y determinista, paginar repite o pierde filas: la fecha empata, así que el Id desempata.
        var items = await query
            .OrderByDescending(t => t.Fecha)
            .ThenBy(t => t.Id)
            .Skip((int)saltar)
            .Take(tamanoPagina)
            .Select(TransaccionDto.Proyeccion)
            .ToListAsync(cancellationToken);

        return new ResultadoPaginado<TransaccionDto>(items, pagina, tamanoPagina, total);
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
}
