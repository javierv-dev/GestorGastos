namespace GestorGastos.Domain.Common;

public enum TipoError
{
    Validacion,
    NoEncontrado,
    Conflicto,
}

// Un fallo esperado del negocio. Se compara por valor: dos errores iguales son el mismo error.
public sealed record ErrorNegocio(string Codigo, string Mensaje, TipoError Tipo, string? Campo = null)
{
    public static ErrorNegocio Validacion(string campo, string codigo, string mensaje) => new(codigo, mensaje, TipoError.Validacion, campo);

    public static ErrorNegocio NoEncontrado(string codigo, string mensaje) => new(codigo, mensaje, TipoError.NoEncontrado);

    public static ErrorNegocio Conflicto(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Conflicto);
}
