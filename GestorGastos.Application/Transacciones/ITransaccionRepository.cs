using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

// Rol de escritura: persistencia del agregado Transaccion (cargar con seguimiento, agregar, eliminar).
public interface ITransaccionRepository
{
    Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Agregar(Transaccion transaccion);

    void Eliminar(Transaccion transaccion);
}
