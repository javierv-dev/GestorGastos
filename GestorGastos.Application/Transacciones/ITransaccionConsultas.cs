using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

// Rol de lectura: consultas de solo lectura que no modifican el estado ni lo siguen.
public interface ITransaccionConsultas
{
    Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaccion>> ListarAsync(
        DateTime? desde,
        DateTime? hasta,
        CategoriaTransaccion? categoria,
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
