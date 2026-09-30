using GestorGastos.Application.Abstractions;

namespace GestorGastos.Application.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int VecesGuardado { get; private set; }

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        VecesGuardado++;
        return Task.FromResult(1);
    }
}
