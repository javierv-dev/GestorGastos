using GestorGastos.Domain.Common;

namespace GestorGastos.Domain.Transacciones;

public static class TransaccionErrors
{
    public static readonly ErrorNegocio MontoInvalido = ErrorNegocio.Validacion(
        "Monto",
        "Transaccion.MontoInvalido",
        "El monto debe ser mayor a cero."
    );

    public static readonly ErrorNegocio TipoInvalido = ErrorNegocio.Validacion(
        "Tipo",
        "Transaccion.TipoInvalido",
        "El tipo debe ser Ingreso o Egreso."
    );

    public static readonly ErrorNegocio DescripcionObligatoria = ErrorNegocio.Validacion(
        "Descripcion",
        "Transaccion.DescripcionObligatoria",
        "La descripción es obligatoria."
    );

    public static readonly ErrorNegocio NoEncontrada = ErrorNegocio.NoEncontrado("Transaccion.NoEncontrada", "La transacción no existe.");
}
