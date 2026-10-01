using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones.Crear;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

public class CrearTransaccionHandlerTests
{
    private static readonly DateTime Fecha = new(2026, 9, 29);

    private readonly FakeTransaccionRepository _repositorio = new();
    private readonly FakePresupuestoRepository _presupuestos = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CrearTransaccionHandler _handler;

    public CrearTransaccionHandlerTests() =>
        _handler = new CrearTransaccionHandler(
            _repositorio,
            ComprobadorFactory.Crear(_repositorio, _presupuestos),
            new PoliticaDeUmbralFijo(),
            _unitOfWork
        );

    private void SembrarIngreso(decimal monto) =>
        _repositorio.Items.Add(Transaccion.Crear("Semilla", monto, TipoTransaccion.Ingreso, null, Fecha).Value);

    private Task<GestorGastos.Domain.Common.Result<CrearTransaccionResultado>> Enviar(decimal monto, TipoTransaccion tipo) =>
        _handler.Handle(new CrearTransaccionCommand("Prueba", monto, tipo, null, Fecha), CancellationToken.None);

    [Fact]
    public async Task ConDatosValidos_AgregaGuardaUnaVezYDevuelveLaTransaccion()
    {
        var resultado = await Enviar(50m, TipoTransaccion.Ingreso);

        Assert.True(resultado.IsSuccess);
        Assert.Single(_repositorio.Agregadas);
        Assert.Same(_repositorio.Agregadas[0], resultado.Value.Transaccion);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task ConDatosInvalidos_FallaSinAgregarNiGuardar()
    {
        var resultado = await Enviar(0m, TipoTransaccion.Egreso);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
        Assert.Empty(_repositorio.Agregadas);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Theory]
    [InlineData(200, 150, true)] // saldo final 50
    [InlineData(200, 100.01, true)] // saldo final 99.99
    [InlineData(200, 100, false)] // saldo final 100: el umbral es "menor que", no "menor o igual"
    [InlineData(200, 50, false)] // saldo final 150
    [InlineData(0, 10, true)] // saldo final negativo
    public async Task Egreso_AvisaSaldoBajoSoloSiElSaldoQuedaPorDebajoDelUmbral(double saldoInicial, double egreso, bool esperado)
    {
        if (saldoInicial > 0)
            SembrarIngreso((decimal)saldoInicial);

        var resultado = await Enviar((decimal)egreso, TipoTransaccion.Egreso);

        Assert.Equal(esperado, resultado.Value.SaldoBajo);
    }

    [Fact]
    public async Task Ingreso_NuncaAvisaSaldoBajo_AunqueElSaldoSigaSiendoBajo()
    {
        var resultado = await Enviar(10m, TipoTransaccion.Ingreso);

        Assert.False(resultado.Value.SaldoBajo);
    }
}
