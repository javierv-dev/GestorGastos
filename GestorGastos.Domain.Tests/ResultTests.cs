using GestorGastos.Domain.Common;

namespace GestorGastos.Domain.Tests;

public class ResultTests
{
    private static readonly ErrorNegocio Error = ErrorNegocio.Validacion("Campo", "Prueba.Error", "Mensaje de prueba.");

    [Fact]
    public void Success_NoTieneError()
    {
        var resultado = Result.Success();

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.IsFailure);
        Assert.Null(resultado.Error);
    }

    [Fact]
    public void Failure_ConservaElError()
    {
        var resultado = Result.Failure(Error);

        Assert.True(resultado.IsFailure);
        Assert.False(resultado.IsSuccess);
        Assert.Same(Error, resultado.Error);
    }

    [Fact]
    public void SuccessGenerico_ExponeElValor()
    {
        var resultado = Result.Success(42);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(42, resultado.Value);
    }

    [Fact]
    public void FailureGenerico_ConservaElError()
    {
        var resultado = Result.Failure<int>(Error);

        Assert.True(resultado.IsFailure);
        Assert.Same(Error, resultado.Error);
    }

    [Fact]
    public void LeerValueDeUnFallo_LanzaInvalidOperationException()
    {
        var resultado = Result.Failure<string>(Error);

        Assert.Throws<InvalidOperationException>(() => resultado.Value);
    }

    [Fact]
    public void ResultGenerico_SePuedeUsarComoResultBase()
    {
        Result resultado = Result.Failure<int>(Error);

        Assert.True(resultado.IsFailure);
    }
}
