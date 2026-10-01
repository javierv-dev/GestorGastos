namespace GestorGastos.Domain.Transacciones;

// Política por defecto: un egreso que deja el saldo por debajo de un umbral fijo es "saldo bajo".
// Quedar exactamente en el umbral no cuenta, y un ingreso nunca avisa.
public sealed class PoliticaDeUmbralFijo : IPoliticaDeSaldoBajo
{
    public const decimal UmbralPorDefecto = 100m;

    private readonly decimal _umbral;

    public PoliticaDeUmbralFijo(decimal umbral = UmbralPorDefecto)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(umbral);
        _umbral = umbral;
    }

    public bool EsSaldoBajo(Transaccion transaccion, decimal saldoResultante) =>
        transaccion.Tipo == TipoTransaccion.Egreso && saldoResultante < _umbral;
}
