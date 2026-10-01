using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests.Fakes;

// Política de prueba: responde lo que se le indica y recuerda con qué datos la consultaron.
// Que el handler funcione con ella, sin cambios, es la prueba de que depende de la abstracción.
public sealed class PoliticaDeSaldoBajoFalsa(bool respuesta) : IPoliticaDeSaldoBajo
{
    public int VecesConsultada { get; private set; }

    public Transaccion? UltimaTransaccion { get; private set; }

    public decimal? UltimoSaldo { get; private set; }

    public bool EsSaldoBajo(Transaccion transaccion, decimal saldoResultante)
    {
        VecesConsultada++;
        UltimaTransaccion = transaccion;
        UltimoSaldo = saldoResultante;
        return respuesta;
    }
}
