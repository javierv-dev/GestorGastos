using GestorGastos.Domain.Common;

namespace GestorGastos.Api.Extensions;

// Traduce un Result del negocio a una respuesta HTTP. Es lo único que conoce HTTP y los tipos de error a la vez.
public static class ResultExtensions
{
    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToHttpResult();

    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToHttpResult();

    private static IResult ToHttpResult(this ErrorNegocio error) =>
        error.Tipo switch
        {
            TipoError.Validacion => Results.ValidationProblem(
                new Dictionary<string, string[]> { [error.Campo ?? error.Codigo] = [error.Mensaje] }
            ),
            TipoError.NoEncontrado => Results.NotFound(),
            TipoError.Conflicto => Results.Problem(title: error.Codigo, detail: error.Mensaje, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(title: error.Codigo, detail: error.Mensaje),
        };
}
