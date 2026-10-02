namespace GestorGastos.Application.Presupuestos;

// Rol de lectura: devuelve modelos de lectura (DTO), nunca entidades. Lo que necesita la entidad para decidir vive en IPresupuestoRepository.
public interface IPresupuestoConsultas
{
    Task<PresupuestoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PresupuestoDto>> ListarAsync(CancellationToken cancellationToken = default);
}
