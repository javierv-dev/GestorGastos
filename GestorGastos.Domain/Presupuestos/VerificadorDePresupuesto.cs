using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Presupuestos;

// Servicio de dominio: una regla que involucra a DOS entidades (Presupuesto y Transaccion) y por eso
// no cabe dentro de ninguna de las dos. No consulta nada ni guarda estado: recibe los datos y decide,
// por eso es estática (no hay nada que inyectar ni que reemplazar).
public static class VerificadorDePresupuesto
{
    public static Result Verificar(Presupuesto? presupuesto, decimal gastadoEnElMes, Transaccion transaccion)
    {
        // Solo los egresos consumen presupuesto.
        if (transaccion.Tipo != TipoTransaccion.Egreso)
            return Result.Success();

        // Sin presupuesto para la categoría no hay límite que respetar.
        if (presupuesto is null)
            return Result.Success();

        // Recibir el presupuesto de otra categoría es un error de quien llama, no una regla de negocio.
        if (presupuesto.Categoria != transaccion.Categoria)
            throw new ArgumentException("El presupuesto no corresponde a la categoría de la transacción.", nameof(presupuesto));

        // Gastar exactamente el límite está permitido; solo se rechaza pasarse.
        return gastadoEnElMes + transaccion.Monto > presupuesto.LimiteMensual
            ? Result.Failure(PresupuestoErrors.Excedido(presupuesto.Categoria, presupuesto.LimiteMensual, gastadoEnElMes))
            : Result.Success();
    }
}
