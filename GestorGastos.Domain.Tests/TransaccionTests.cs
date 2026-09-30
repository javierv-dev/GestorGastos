using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Tests;

public class TransaccionTests
{
    private static readonly DateTime Fecha = new(2026, 9, 29);

    [Fact]
    public void Crear_ConDatosValidos_DevuelveExitoConLosValoresAsignados()
    {
        var resultado = Transaccion.Crear("Sueldo", 1000m, TipoTransaccion.Ingreso, CategoriaTransaccion.Salario, Fecha);

        Assert.True(resultado.IsSuccess);
        var transaccion = resultado.Value;
        Assert.NotEqual(Guid.Empty, transaccion.Id);
        Assert.Equal("Sueldo", transaccion.Descripcion);
        Assert.Equal(1000m, transaccion.Monto);
        Assert.Equal(TipoTransaccion.Ingreso, transaccion.Tipo);
        Assert.Equal(CategoriaTransaccion.Salario, transaccion.Categoria);
        Assert.Equal(Fecha, transaccion.Fecha);
    }

    [Fact]
    public void Crear_SinCategoria_UsaOtros()
    {
        var resultado = Transaccion.Crear("Varios", 10m, TipoTransaccion.Egreso, null, Fecha);

        Assert.Equal(CategoriaTransaccion.Otros, resultado.Value.Categoria);
    }

    [Fact]
    public void Crear_GeneraUnIdDistintoEnCadaLlamada()
    {
        var primera = Transaccion.Crear("A", 1m, TipoTransaccion.Ingreso, null, Fecha).Value;
        var segunda = Transaccion.Crear("A", 1m, TipoTransaccion.Ingreso, null, Fecha).Value;

        Assert.NotEqual(primera.Id, segunda.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Crear_ConMontoNoPositivo_FallaConMontoInvalido(double monto)
    {
        var resultado = Transaccion.Crear("X", (decimal)monto, TipoTransaccion.Egreso, null, Fecha);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
    }

    [Fact]
    public void Crear_ConMontoMinimoPositivo_Funciona()
    {
        var resultado = Transaccion.Crear("X", 0.01m, TipoTransaccion.Egreso, null, Fecha);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public void Crear_ConTipoFueraDelEnum_FallaConTipoInvalido()
    {
        var resultado = Transaccion.Crear("X", 5m, (TipoTransaccion)99, null, Fecha);

        Assert.Equal(TransaccionErrors.TipoInvalido, resultado.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Crear_ConDescripcionVacia_FallaConDescripcionObligatoria(string descripcion)
    {
        var resultado = Transaccion.Crear(descripcion, 5m, TipoTransaccion.Egreso, null, Fecha);

        Assert.Equal(TransaccionErrors.DescripcionObligatoria, resultado.Error);
    }

    [Fact]
    public void Crear_ConVariosErrores_ReportaPrimeroElMonto()
    {
        var resultado = Transaccion.Crear("", 0m, (TipoTransaccion)99, null, Fecha);

        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
    }

    [Fact]
    public void Actualizar_ConDatosValidos_CambiaLosValoresYConservaElId()
    {
        var transaccion = Transaccion.Crear("Original", 10m, TipoTransaccion.Egreso, CategoriaTransaccion.Salud, Fecha).Value;
        var id = transaccion.Id;
        var nuevaFecha = Fecha.AddDays(1);

        var resultado = transaccion.Actualizar("Nueva", 20m, TipoTransaccion.Ingreso, null, nuevaFecha);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(id, transaccion.Id);
        Assert.Equal("Nueva", transaccion.Descripcion);
        Assert.Equal(20m, transaccion.Monto);
        Assert.Equal(TipoTransaccion.Ingreso, transaccion.Tipo);
        Assert.Equal(CategoriaTransaccion.Otros, transaccion.Categoria);
        Assert.Equal(nuevaFecha, transaccion.Fecha);
    }

    [Fact]
    public void Actualizar_ConMontoInvalido_FallaYNoModificaNada()
    {
        var transaccion = Transaccion.Crear("Original", 10m, TipoTransaccion.Egreso, CategoriaTransaccion.Salud, Fecha).Value;

        var resultado = transaccion.Actualizar("Nueva", -5m, TipoTransaccion.Ingreso, CategoriaTransaccion.Salario, Fecha.AddDays(1));

        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
        AssertSinCambios(transaccion);
    }

    [Fact]
    public void Actualizar_ConDescripcionVacia_FallaYNoModificaNada()
    {
        var transaccion = Transaccion.Crear("Original", 10m, TipoTransaccion.Egreso, CategoriaTransaccion.Salud, Fecha).Value;

        var resultado = transaccion.Actualizar(" ", 99m, TipoTransaccion.Ingreso, CategoriaTransaccion.Salario, Fecha.AddDays(1));

        Assert.Equal(TransaccionErrors.DescripcionObligatoria, resultado.Error);
        AssertSinCambios(transaccion);
    }

    [Fact]
    public void Actualizar_ConTipoInvalido_FallaYNoModificaNada()
    {
        var transaccion = Transaccion.Crear("Original", 10m, TipoTransaccion.Egreso, CategoriaTransaccion.Salud, Fecha).Value;

        var resultado = transaccion.Actualizar("Nueva", 99m, (TipoTransaccion)7, CategoriaTransaccion.Salario, Fecha.AddDays(1));

        Assert.Equal(TransaccionErrors.TipoInvalido, resultado.Error);
        AssertSinCambios(transaccion);
    }

    private static void AssertSinCambios(Transaccion transaccion)
    {
        Assert.Equal("Original", transaccion.Descripcion);
        Assert.Equal(10m, transaccion.Monto);
        Assert.Equal(TipoTransaccion.Egreso, transaccion.Tipo);
        Assert.Equal(CategoriaTransaccion.Salud, transaccion.Categoria);
        Assert.Equal(Fecha, transaccion.Fecha);
    }
}
