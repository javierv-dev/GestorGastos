using GestorGastos.Application.Common;

namespace GestorGastos.Api.Dtos;

public record PaginaResponse<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, int Total, int TotalPaginas);

public static class PaginaResponse
{
    public static PaginaResponse<T> Desde<TOrigen, T>(ResultadoPaginado<TOrigen> pagina, Func<TOrigen, T> mapear) =>
        new([.. pagina.Items.Select(mapear)], pagina.Pagina, pagina.TamanoPagina, pagina.Total, pagina.TotalPaginas);
}
