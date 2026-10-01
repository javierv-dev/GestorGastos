using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Dtos;

public record CrearPresupuestoRequest(CategoriaTransaccion Categoria, decimal LimiteMensual);

public record ActualizarPresupuestoRequest(decimal LimiteMensual);

public record PresupuestoResponse(Guid Id, CategoriaTransaccion Categoria, decimal LimiteMensual)
{
    public static PresupuestoResponse Desde(Presupuesto presupuesto) =>
        new(presupuesto.Id, presupuesto.Categoria, presupuesto.LimiteMensual);
}
