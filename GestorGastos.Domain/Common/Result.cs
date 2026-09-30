using System.Diagnostics.CodeAnalysis;

namespace GestorGastos.Domain.Common;

// Resultado de una operación que puede fallar por una regla de negocio, sin lanzar excepciones.
public class Result
{
    protected Result(ErrorNegocio? error) => Error = error;

    public ErrorNegocio? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(ErrorNegocio error) => new(error);

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(ErrorNegocio error) => new(default, error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, ErrorNegocio? error)
        : base(error) => _value = value;

    // Leer el valor de un resultado fallido es un error de programación, no una regla de negocio.
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");
}
