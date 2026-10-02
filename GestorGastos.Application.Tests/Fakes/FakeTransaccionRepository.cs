using GestorGastos.Application.Common;
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

    // Rol de escritura: devuelve la entidad.
    public Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    // Rol de lectura: devuelve el DTO. Misma firma de parámetros que el anterior, por eso se implementa de forma explícita.
    Task<TransaccionDto?> ITransaccionConsultas.ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Where(t => t.Id == id).Select(TransaccionDto.Desde).FirstOrDefault());

    public (int Pagina, int TamanoPagina)? UltimaPaginacion { get; private set; }

    public int Consultas { get; private set; }

    public Task<ResultadoPaginado<TransaccionDto>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default
    )
    {
        Consultas++;
        UltimoFiltro = (desde, hasta, categoria);
        UltimaPaginacion = (pagina, tamanoPagina);
        var items = Items.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).Select(TransaccionDto.Desde).ToList();
        return Task.FromResult(new ResultadoPaginado<TransaccionDto>(items, pagina, tamanoPagina, Items.Count));
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
