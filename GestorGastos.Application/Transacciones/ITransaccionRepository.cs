using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

public interface ITransaccionRepository
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

    void Agregar(Transaccion transaccion);

    void Eliminar(Transaccion transaccion);
}
