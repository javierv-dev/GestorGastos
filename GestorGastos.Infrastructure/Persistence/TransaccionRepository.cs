using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Infrastructure.Persistence;

public class TransaccionRepository(GestorGastosDbContext db) : ITransaccionRepository
{
    public async Task<Transaccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Transacciones.FindAsync([id], cancellationToken);

    public void Agregar(Transaccion transaccion) => db.Transacciones.Add(transaccion);

    public void Eliminar(Transaccion transaccion) => db.Transacciones.Remove(transaccion);
}
