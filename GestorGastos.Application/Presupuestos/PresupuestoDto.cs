using System.Linq.Expressions;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Presupuestos;

// Modelo de lectura: lo que Application expone de un presupuesto. La entidad nunca sale de esta capa.
public sealed record PresupuestoDto(Guid Id, CategoriaTransaccion Categoria, decimal LimiteMensual)
{
    public static readonly Expression<Func<Presupuesto, PresupuestoDto>> Proyeccion = p => new PresupuestoDto(
        p.Id,
        p.Categoria,
        p.LimiteMensual
    );

    private static readonly Func<Presupuesto, PresupuestoDto> Mapeador = Proyeccion.Compile();

    public static PresupuestoDto Desde(Presupuesto presupuesto) => Mapeador(presupuesto);
}
