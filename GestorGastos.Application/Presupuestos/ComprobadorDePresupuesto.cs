using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Presupuestos;

// Servicio de aplicación: orquesta. Busca los datos (presupuesto y gasto del mes) y se los entrega al
// servicio de dominio, que es quien decide. Lo usan los handlers que crean o modifican egresos.
public class ComprobadorDePresupuesto(ITransaccionConsultas transacciones, IPresupuestoConsultas presupuestos)
{
    public async Task<Result> ComprobarAsync(Transaccion transaccion, Guid? excluirTransaccionId, CancellationToken cancellationToken)
    {
        // Optimización: un ingreso nunca consume presupuesto, así que ni se consulta la base de datos.
        if (transaccion.Tipo != TipoTransaccion.Egreso)
            return Result.Success();

        var presupuesto = await presupuestos.ObtenerPorCategoriaAsync(transaccion.Categoria, cancellationToken);
        if (presupuesto is null)
            return Result.Success();

        var gastado = await transacciones.ObtenerEgresosDelMesAsync(
            transaccion.Categoria,
            transaccion.Fecha,
            excluirTransaccionId,
            cancellationToken
        );

        return VerificadorDePresupuesto.Verificar(presupuesto, gastado, transaccion);
    }
}
