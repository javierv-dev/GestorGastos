using GestorGastos.Application.Abstractions;

namespace GestorGastos.Infrastructure.Persistence;

public class UnitOfWork(GestorGastosDbContext db) : IUnitOfWork
{
    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
