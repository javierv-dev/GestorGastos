namespace GestorGastos.Application.Abstractions;

// Los repositorios registran cambios; la unidad de trabajo los confirma todos juntos.
public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
