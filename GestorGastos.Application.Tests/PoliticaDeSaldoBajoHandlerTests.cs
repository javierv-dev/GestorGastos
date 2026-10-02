using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones;
using GestorGastos.Application.Transacciones.Crear;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

// Principio abierto/cerrado e inversión de dependencias: el handler de crear acepta CUALQUIER política de saldo bajo
// sin modificarse. Estas pruebas la reemplazan por una falsa que responde lo contrario de lo que diría el umbral de 100.
public class PoliticaDeSaldoBajoHandlerTests
{
    private static readonly DateTime Fecha = new(2026, 9, 15);

    private readonly FakeTransaccionRepository _transacciones = new();
    private readonly FakePresupuestoRepository _presupuestos = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task<GestorGastos.Domain.Common.Result<CrearTransaccionResultado>> Crear(
        IPoliticaDeSaldoBajo politica,
        decimal monto,
        TipoTransaccion tipo
    ) =>
        new CrearTransaccionHandler(
            _transacciones,
            _transacciones,
            ComprobadorFactory.Crear(_transacciones, _presupuestos),
            politica,
            _unitOfWork
        ).Handle(new CrearTransaccionCommand("Prueba", monto, tipo, null, Fecha), CancellationToken.None);

    [Fact]
    public async Task ElHandler_UsaElVeredictoDeLaPolitica_AunqueContradigaAlUmbralPorDefecto()
    {
        // Un ingreso nunca avisaría con la política por defecto; la falsa dice que sí.
        var siempreSi = new PoliticaDeSaldoBajoFalsa(respuesta: true);
        Assert.True((await Crear(siempreSi, 1_000m, TipoTransaccion.Ingreso)).Value.SaldoBajo);

        // Un egreso que deja 50 de saldo avisaría con la política por defecto; la falsa dice que no.
        var siempreNo = new PoliticaDeSaldoBajoFalsa(respuesta: false);
        _transacciones.Items.Clear();
        _transacciones.Items.Add(Transaccion.Crear("Semilla", 100m, TipoTransaccion.Ingreso, null, Fecha).Value);
        Assert.False((await Crear(siempreNo, 50m, TipoTransaccion.Egreso)).Value.SaldoBajo);
    }

    [Fact]
    public async Task ElHandler_LeEntregaALaPoliticaLaTransaccionCreadaYElSaldoYaGuardado()
    {
        _transacciones.Items.Add(Transaccion.Crear("Semilla", 300m, TipoTransaccion.Ingreso, null, Fecha).Value);
        var politica = new PoliticaDeSaldoBajoFalsa(respuesta: false);

        var resultado = await Crear(politica, 50m, TipoTransaccion.Egreso);

        Assert.Equal(1, politica.VecesConsultada);
        Assert.Equal(TransaccionDto.Desde(politica.UltimaTransaccion!), resultado.Value.Transaccion);
        Assert.Equal(250m, politica.UltimoSaldo);
    }

    [Fact]
    public async Task ElHandler_NoConsultaLaPolitica_SiLaCreacionFalla()
    {
        var politica = new PoliticaDeSaldoBajoFalsa(respuesta: true);

        var resultado = await Crear(politica, 0m, TipoTransaccion.Egreso);

        Assert.True(resultado.IsFailure);
        Assert.Equal(0, politica.VecesConsultada);
    }

    [Fact]
    public async Task ConLaPoliticaRealYOtroUmbral_ElMismoEgresoCambiaDeVeredicto()
    {
        _transacciones.Items.Add(Transaccion.Crear("Semilla", 400m, TipoTransaccion.Ingreso, null, Fecha).Value);

        // Saldo resultante: 300. Con el umbral por defecto (100) no avisa; con uno de 500 sí.
        var normal = await Crear(new PoliticaDeUmbralFijo(), 100m, TipoTransaccion.Egreso);
        _transacciones.Items.Clear();
        _transacciones.Items.Add(Transaccion.Crear("Semilla", 400m, TipoTransaccion.Ingreso, null, Fecha).Value);
        var estricta = await Crear(new PoliticaDeUmbralFijo(500m), 100m, TipoTransaccion.Egreso);

        Assert.False(normal.Value.SaldoBajo);
        Assert.True(estricta.Value.SaldoBajo);
    }
}
