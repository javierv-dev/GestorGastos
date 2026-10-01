using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests.Fakes;

// Repositorio en memoria: registra lo que los handlers le piden sin tocar ninguna base de datos.
public sealed class FakePresupuestoRepository : IPresupuestoRepository
{
    public List<Presupuesto> Items { get; } = [];

    public List<Presupuesto> Agregados { get; } = [];

    public List<Presupuesto> Eliminados { get; } = [];

    public Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Categoria == categoria));

    public Task<IReadOnlyList<Presupuesto>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Presupuesto>>(Items.OrderBy(p => p.Categoria).ToList());

    public void Agregar(Presupuesto presupuesto)
    {
        Items.Add(presupuesto);
        Agregados.Add(presupuesto);
    }

    public void Eliminar(Presupuesto presupuesto)
    {
        Items.Remove(presupuesto);
        Eliminados.Add(presupuesto);
    }
}
