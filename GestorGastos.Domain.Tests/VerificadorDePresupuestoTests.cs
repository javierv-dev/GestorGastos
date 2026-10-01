using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Tests;

public class VerificadorDePresupuestoTests
{
    private static readonly DateTime Fecha = new(2026, 9, 15);

    private static Presupuesto PresupuestoDe(CategoriaTransaccion categoria, decimal limite) => Presupuesto.Crear(categoria, limite).Value;

    private static Transaccion Egreso(decimal monto, CategoriaTransaccion categoria = CategoriaTransaccion.Alimentacion) =>
        Transaccion.Crear("Compra", monto, TipoTransaccion.Egreso, categoria, Fecha).Value;

    [Fact]
    public void Egreso_DentroDelLimite_EsAceptado()
    {
        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m), 100m, Egreso(50m));

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public void Egreso_QueLlegaExactamenteAlLimite_EsAceptado()
    {
        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m), 350m, Egreso(50m));

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public void Egreso_UnCentavoPorEncimaDelLimite_EsRechazadoComoConflicto()
    {
        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m), 350m, Egreso(50.01m));

        Assert.True(resultado.IsFailure);
        Assert.Equal(TipoError.Conflicto, resultado.Error.Tipo);
        Assert.Equal("Presupuesto.Excedido", resultado.Error.Codigo);
    }

    [Theory]
    [InlineData(0, 400, true)] // justo el límite, sin gasto previo
    [InlineData(0, 400.01, false)]
    [InlineData(399.99, 0.01, true)]
    [InlineData(399.99, 0.02, false)]
    [InlineData(400, 0.01, false)] // el límite ya estaba agotado
    public void Egreso_ConDistintosMontos_RespetaElBordeDelLimite(double gastado, double monto, bool esperadoAceptado)
    {
        var resultado = VerificadorDePresupuesto.Verificar(
            PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m),
            (decimal)gastado,
            Egreso((decimal)monto)
        );

        Assert.Equal(esperadoAceptado, resultado.IsSuccess);
    }

    [Fact]
    public void Egreso_SinPresupuestoParaLaCategoria_EsAceptadoSinImportarElMonto()
    {
        var resultado = VerificadorDePresupuesto.Verificar(null, 1_000_000m, Egreso(999_999m));

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public void Ingreso_NuncaSeVerifica_AunqueElPresupuestoEsteAgotado()
    {
        var ingreso = Transaccion.Crear("Reembolso", 5_000m, TipoTransaccion.Ingreso, CategoriaTransaccion.Alimentacion, Fecha).Value;

        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m), 400m, ingreso);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public void ElMensajeDelRechazo_IndicaLimiteGastadoYDisponible()
    {
        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 400m), 380m, Egreso(50m));

        Assert.True(resultado.IsFailure);
        Assert.Equal(
            "El egreso excede el presupuesto mensual de Alimentacion: límite 400.00, ya gastado 380.00, disponible 20.00.",
            resultado.Error.Mensaje
        );
    }

    [Fact]
    public void ElMensajeDelRechazo_NuncaMuestraUnDisponibleNegativo()
    {
        // Ya se había gastado más que el límite (por ejemplo, porque luego se bajó el límite).
        var resultado = VerificadorDePresupuesto.Verificar(PresupuestoDe(CategoriaTransaccion.Alimentacion, 300m), 450m, Egreso(10m));

        Assert.True(resultado.IsFailure);
        Assert.Contains("disponible 0.00", resultado.Error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void ElErrorDeExceso_EsIgualParaLosMismosDatos()
    {
        Assert.Equal(
            PresupuestoErrors.Excedido(CategoriaTransaccion.Salud, 100m, 60m),
            PresupuestoErrors.Excedido(CategoriaTransaccion.Salud, 100m, 60m)
        );
        Assert.NotEqual(
            PresupuestoErrors.Excedido(CategoriaTransaccion.Salud, 100m, 60m),
            PresupuestoErrors.Excedido(CategoriaTransaccion.Salud, 100m, 70m)
        );
    }

    [Fact]
    public void ConElPresupuestoDeOtraCategoria_LanzaArgumentException_PorqueEsUnErrorDeQuienLlama()
    {
        var presupuestoDeVivienda = PresupuestoDe(CategoriaTransaccion.Vivienda, 800m);

        Assert.Throws<ArgumentException>(() =>
            VerificadorDePresupuesto.Verificar(presupuestoDeVivienda, 0m, Egreso(10m, CategoriaTransaccion.Alimentacion))
        );
    }
}
