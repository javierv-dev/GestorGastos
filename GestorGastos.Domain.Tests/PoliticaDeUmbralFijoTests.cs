using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Tests;

public class PoliticaDeUmbralFijoTests
{
    private static readonly DateTime Fecha = new(2026, 9, 15);

    private static Transaccion Egreso() => Transaccion.Crear("Compra", 10m, TipoTransaccion.Egreso, null, Fecha).Value;

    private static Transaccion Ingreso() => Transaccion.Crear("Cobro", 10m, TipoTransaccion.Ingreso, null, Fecha).Value;

    [Fact]
    public void ElUmbralPorDefecto_Es100()
    {
        Assert.Equal(100m, PoliticaDeUmbralFijo.UmbralPorDefecto);
    }

    [Theory]
    [InlineData(99.99, true)]
    [InlineData(50, true)]
    [InlineData(0, true)]
    [InlineData(-30, true)]
    [InlineData(100, false)] // quedar exactamente en el umbral NO cuenta como saldo bajo
    [InlineData(100.01, false)]
    [InlineData(5000, false)]
    public void Egreso_AvisaSoloSiElSaldoQuedaPorDebajoDelUmbralPorDefecto(double saldoResultante, bool esperado)
    {
        var politica = new PoliticaDeUmbralFijo();

        Assert.Equal(esperado, politica.EsSaldoBajo(Egreso(), (decimal)saldoResultante));
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(5000)]
    public void Ingreso_NuncaAvisa_SinImportarElSaldo(double saldoResultante)
    {
        var politica = new PoliticaDeUmbralFijo();

        Assert.False(politica.EsSaldoBajo(Ingreso(), (decimal)saldoResultante));
    }

    [Fact]
    public void ConOtroUmbral_ElMismoSaldoCambiaDeVeredicto()
    {
        var porDefecto = new PoliticaDeUmbralFijo();
        var estricta = new PoliticaDeUmbralFijo(500m);

        Assert.False(porDefecto.EsSaldoBajo(Egreso(), 300m));
        Assert.True(estricta.EsSaldoBajo(Egreso(), 300m));
    }

    [Fact]
    public void ConUmbralCero_SoloAvisaCuandoElSaldoQuedaNegativo()
    {
        var politica = new PoliticaDeUmbralFijo(0m);

        Assert.True(politica.EsSaldoBajo(Egreso(), -0.01m));
        Assert.False(politica.EsSaldoBajo(Egreso(), 0m));
    }

    [Fact]
    public void ConUmbralNegativo_LanzaArgumentOutOfRangeException_PorqueEsUnErrorDeConfiguracion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaDeUmbralFijo(-1m));
    }

    [Fact]
    public void EsUnaIPoliticaDeSaldoBajo_ParaQuePuedaSustituirseSinTocarAQuienLaUsa()
    {
        Assert.IsAssignableFrom<IPoliticaDeSaldoBajo>(new PoliticaDeUmbralFijo());
    }
}
