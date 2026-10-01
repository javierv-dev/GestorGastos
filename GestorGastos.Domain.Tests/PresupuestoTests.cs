using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Tests;

public class PresupuestoTests
{
    [Fact]
    public void Crear_ConDatosValidos_DevuelveExitoConLosValoresAsignados()
    {
        var resultado = Presupuesto.Crear(CategoriaTransaccion.Alimentacion, 400m);

        Assert.True(resultado.IsSuccess);
        Assert.NotEqual(Guid.Empty, resultado.Value.Id);
        Assert.Equal(CategoriaTransaccion.Alimentacion, resultado.Value.Categoria);
        Assert.Equal(400m, resultado.Value.LimiteMensual);
    }

    [Theory]
    [InlineData(CategoriaTransaccion.Alimentacion)]
    [InlineData(CategoriaTransaccion.Transporte)]
    [InlineData(CategoriaTransaccion.Vivienda)]
    [InlineData(CategoriaTransaccion.Servicios)]
    [InlineData(CategoriaTransaccion.Salud)]
    [InlineData(CategoriaTransaccion.Entretenimiento)]
    [InlineData(CategoriaTransaccion.Educacion)]
    [InlineData(CategoriaTransaccion.Otros)]
    public void Crear_ConCualquierCategoriaDeGasto_Funciona(CategoriaTransaccion categoria)
    {
        Assert.True(Presupuesto.Crear(categoria, 100m).IsSuccess);
    }

    [Fact]
    public void Crear_ConCategoriaSalario_FallaPorqueEsUnaFuenteDeIngresos()
    {
        var resultado = Presupuesto.Crear(CategoriaTransaccion.Salario, 100m);

        Assert.Equal(PresupuestoErrors.CategoriaNoPresupuestable, resultado.Error);
    }

    [Fact]
    public void Crear_ConCategoriaFueraDelEnum_FallaConCategoriaInvalida()
    {
        var resultado = Presupuesto.Crear((CategoriaTransaccion)99, 100m);

        Assert.Equal(PresupuestoErrors.CategoriaInvalida, resultado.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Crear_ConLimiteNoPositivo_FallaConLimiteInvalido(double limite)
    {
        var resultado = Presupuesto.Crear(CategoriaTransaccion.Salud, (decimal)limite);

        Assert.Equal(PresupuestoErrors.LimiteInvalido, resultado.Error);
    }

    [Fact]
    public void Crear_ConLimiteMinimoPositivo_Funciona()
    {
        Assert.True(Presupuesto.Crear(CategoriaTransaccion.Salud, 0.01m).IsSuccess);
    }

    [Fact]
    public void Crear_ConVariosErrores_ReportaPrimeroLaCategoria()
    {
        var resultado = Presupuesto.Crear(CategoriaTransaccion.Salario, 0m);

        Assert.Equal(PresupuestoErrors.CategoriaNoPresupuestable, resultado.Error);
    }

    [Fact]
    public void Crear_GeneraUnIdDistintoEnCadaLlamada()
    {
        var primero = Presupuesto.Crear(CategoriaTransaccion.Salud, 10m).Value;
        var segundo = Presupuesto.Crear(CategoriaTransaccion.Salud, 10m).Value;

        Assert.NotEqual(primero.Id, segundo.Id);
    }

    [Fact]
    public void CambiarLimite_ConValorValido_ActualizaSoloElLimite()
    {
        var presupuesto = Presupuesto.Crear(CategoriaTransaccion.Vivienda, 800m).Value;
        var id = presupuesto.Id;

        var resultado = presupuesto.CambiarLimite(950m);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(950m, presupuesto.LimiteMensual);
        Assert.Equal(CategoriaTransaccion.Vivienda, presupuesto.Categoria);
        Assert.Equal(id, presupuesto.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void CambiarLimite_ConValorInvalido_FallaYNoModificaNada(double nuevoLimite)
    {
        var presupuesto = Presupuesto.Crear(CategoriaTransaccion.Vivienda, 800m).Value;

        var resultado = presupuesto.CambiarLimite((decimal)nuevoLimite);

        Assert.Equal(PresupuestoErrors.LimiteInvalido, resultado.Error);
        Assert.Equal(800m, presupuesto.LimiteMensual);
    }

    [Fact]
    public void ErroresDePresupuesto_TienenElTipoYCampoEsperados()
    {
        Assert.Equal(Common.TipoError.Validacion, PresupuestoErrors.LimiteInvalido.Tipo);
        Assert.Equal("LimiteMensual", PresupuestoErrors.LimiteInvalido.Campo);
        Assert.Equal("Categoria", PresupuestoErrors.CategoriaNoPresupuestable.Campo);
        Assert.Equal(Common.TipoError.NoEncontrado, PresupuestoErrors.NoEncontrado.Tipo);
        Assert.Equal(Common.TipoError.Conflicto, PresupuestoErrors.YaExiste.Tipo);
    }
}
