using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Dtos;

public record CrearPresupuestoRequest(CategoriaTransaccion Categoria, decimal LimiteMensual);

public record ActualizarPresupuestoRequest(decimal LimiteMensual);

// El contrato HTTP. Se mantiene aparte del DTO de Application para poder cambiar uno sin romper al otro.
public record PresupuestoResponse(Guid Id, CategoriaTransaccion Categoria, decimal LimiteMensual)
{
    public static PresupuestoResponse Desde(PresupuestoDto presupuesto) =>
        new(presupuesto.Id, presupuesto.Categoria, presupuesto.LimiteMensual);
}
