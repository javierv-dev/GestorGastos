using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests.Fakes;

// Repositorio en memoria con los dos roles (escritura y consultas): registra lo que los handlers le piden sin tocar ninguna base de datos.
public sealed class FakeTransaccionRepository : ITransaccionRepository, ITransaccionConsultas
{
    public List<Transaccion> Items { get; } = [];

    public List<Transaccion> Agregadas { get; } = [];

    public List<Transaccion> Eliminadas { get; } = [];

    public (DateTime? Desde, DateTime? Hasta, CategoriaTransaccion? Categoria)? UltimoFiltro { get; private set; }

    public IReadOnlyList<ResumenCategoria> Resumen { get; set; } = [];

    public Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Transaccion>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        CancellationToken cancellationToken = default
    )
    {
        UltimoFiltro = (desde, hasta, categoria);
        return Task.FromResult<IReadOnlyList<Transaccion>>(Items.ToList());
    }

    public Task<decimal> ObtenerSaldoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Sum(t => t.Tipo == TipoTransaccion.Ingreso ? t.Monto : -t.Monto));

    public Task<decimal> ObtenerEgresosDelMesAsync(
        CategoriaTransaccion categoria,
        DateTime fecha,
        Guid? excluirTransaccionId = null,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            Items
                .Where(t =>
                    t.Tipo == TipoTransaccion.Egreso
                    && t.Categoria == categoria
                    && t.Fecha.Year == fecha.Year
                    && t.Fecha.Month == fecha.Month
                    && t.Id != excluirTransaccionId
                )
                .Sum(t => t.Monto)
        );

    public Task<IReadOnlyList<ResumenCategoria>> ObtenerResumenPorCategoriaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Resumen);

    public void Agregar(Transaccion transaccion)
    {
        Items.Add(transaccion);
        Agregadas.Add(transaccion);
    }

    public void Eliminar(Transaccion transaccion)
    {
        Items.Remove(transaccion);
        Eliminadas.Add(transaccion);
    }
}
