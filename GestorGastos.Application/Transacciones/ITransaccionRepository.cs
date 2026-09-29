using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

public interface ITransaccionRepository
{
    Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaccion>> ListarAsync(
        DateTime? desde, DateTime? hasta, CategoriaTransaccion? categoria,
        CancellationToken cancellationToken = default);

    Task<decimal> ObtenerSaldoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResumenCategoria>> ObtenerResumenPorCategoriaAsync(
        CancellationToken cancellationToken = default);

    void Agregar(Transaccion transaccion);

    void Eliminar(Transaccion transaccion);
}
