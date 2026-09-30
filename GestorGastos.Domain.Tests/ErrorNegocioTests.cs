using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Tests;

public class ErrorNegocioTests
{
    [Fact]
    public void Validacion_AsignaTipoYCampo()
    {
        var error = ErrorNegocio.Validacion("Monto", "X.Codigo", "Mensaje");

        Assert.Equal(TipoError.Validacion, error.Tipo);
        Assert.Equal("Monto", error.Campo);
        Assert.Equal("X.Codigo", error.Codigo);
        Assert.Equal("Mensaje", error.Mensaje);
    }

    [Fact]
    public void NoEncontrado_AsignaTipoYNoTieneCampo()
    {
        var error = ErrorNegocio.NoEncontrado("X.NoEncontrado", "No existe");

        Assert.Equal(TipoError.NoEncontrado, error.Tipo);
        Assert.Null(error.Campo);
    }

    [Fact]
    public void Conflicto_AsignaTipo()
    {
        var error = ErrorNegocio.Conflicto("X.Conflicto", "Choca");

        Assert.Equal(TipoError.Conflicto, error.Tipo);
    }

    [Fact]
    public void DosErroresConLosMismosDatos_SonIguales()
    {
        Assert.Equal(ErrorNegocio.Validacion("A", "B", "C"), ErrorNegocio.Validacion("A", "B", "C"));
        Assert.NotEqual(ErrorNegocio.Validacion("A", "B", "C"), ErrorNegocio.Validacion("A", "B", "otro"));
    }

    [Fact]
    public void ErroresDeTransaccion_TienenElTipoYCampoEsperados()
    {
        Assert.Equal(TipoError.Validacion, TransaccionErrors.MontoInvalido.Tipo);
        Assert.Equal("Monto", TransaccionErrors.MontoInvalido.Campo);
        Assert.Equal(TipoError.Validacion, TransaccionErrors.TipoInvalido.Tipo);
        Assert.Equal("Tipo", TransaccionErrors.TipoInvalido.Campo);
        Assert.Equal(TipoError.Validacion, TransaccionErrors.DescripcionObligatoria.Tipo);
        Assert.Equal("Descripcion", TransaccionErrors.DescripcionObligatoria.Campo);
        Assert.Equal(TipoError.NoEncontrado, TransaccionErrors.NoEncontrada.Tipo);
    }
}
