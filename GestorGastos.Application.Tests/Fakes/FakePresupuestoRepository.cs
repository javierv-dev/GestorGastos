using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests.Fakes;

// Repositorio en memoria con los dos roles (escritura y consultas): registra lo que los handlers le piden sin tocar ninguna base de datos.
public sealed class FakePresupuestoRepository : IPresupuestoRepository, IPresupuestoConsultas
{
    public List<Presupuesto> Items { get; } = [];

    public List<Presupuesto> Agregados { get; } = [];

    public List<Presupuesto> Eliminados { get; } = [];

    // Rol de escritura: devuelve la entidad.
    public Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    // Rol de lectura: devuelve el DTO. Misma firma de parámetros que el anterior, por eso se implementa de forma explícita.
    Task<PresupuestoDto?> IPresupuestoConsultas.ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Where(p => p.Id == id).Select(PresupuestoDto.Desde).FirstOrDefault());

    public Task<Presupuesto?> ObtenerPorCategoriaAsync(CategoriaTransaccion categoria, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Categoria == categoria));

    public Task<IReadOnlyList<PresupuestoDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PresupuestoDto>>(Items.OrderBy(p => p.Categoria).Select(PresupuestoDto.Desde).ToList());

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
