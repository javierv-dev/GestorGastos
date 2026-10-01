namespace GestorGastos.Domain.Transacciones;

// Decide cuándo una transacción deja al usuario con un saldo preocupante. Es una abstracción para que la regla
// pueda cambiar (otro umbral, un porcentaje de los ingresos, etc.) sin tocar quien la usa.
public interface IPoliticaDeSaldoBajo
{
    bool EsSaldoBajo(Transaccion transaccion, decimal saldoResultante);
}
