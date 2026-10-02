namespace GestorGastos.Application.Common;

// Una página de resultados más lo necesario para que el cliente navegue: en qué página está y cuántos elementos hay en total.
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TamanoPagina);
}
