using GestorGastos.Domain.Common;

namespace GestorGastos.Application.Common;

public static class Paginacion
{
    public const int PaginaInicial = 1;
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public static readonly ErrorNegocio PaginaInvalida = ErrorNegocio.Validacion(
        "Pagina",
        "Paginacion.PaginaInvalida",
        "La página debe ser mayor o igual a 1."
    );

    public static readonly ErrorNegocio TamanoPaginaInvalido = ErrorNegocio.Validacion(
        "TamanoPagina",
        "Paginacion.TamanoPaginaInvalido",
        $"El tamaño de página debe estar entre 1 y {TamanoMaximo}."
    );

    // Devuelve el primer error de paginación, o null si los valores son válidos.
    public static ErrorNegocio? Validar(int pagina, int tamanoPagina) =>
        pagina < PaginaInicial ? PaginaInvalida
        : tamanoPagina is < 1 or > TamanoMaximo ? TamanoPaginaInvalido
        : null;
}
