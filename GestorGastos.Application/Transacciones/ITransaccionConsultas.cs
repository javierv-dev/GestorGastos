using GestorGastos.Application.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

// Rol de lectura: consultas de solo lectura que devuelven modelos de lectura (DTO), nunca entidades.
public interface ITransaccionConsultas
{
    Task<TransaccionDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Una página del listado, de la más reciente a la más antigua. Los filtros se aplican antes de contar y paginar.
    Task<ResultadoPaginado<TransaccionDto>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default
    );

    Task<decimal> ObtenerSaldoAsync(CancellationToken cancellationToken = default);

    // Suma los egresos de la categoría en el mes calendario de la fecha dada, sin contar la transacción excluida.
    Task<decimal> ObtenerEgresosDelMesAsync(
        CategoriaTransaccion categoria,
        DateTime fecha,
        Guid? excluirTransaccionId = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ResumenCategoria>> ObtenerResumenPorCategoriaAsync(CancellationToken cancellationToken = default);
}
