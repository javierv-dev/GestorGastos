using GestorGastos.Application.Common;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Listar;

public record ListarTransaccionesQuery(
    DateTime? Desde,
    DateTime? Hasta,
    CategoriaTransaccion? Categoria,
    int Pagina = Paginacion.PaginaInicial,
    int TamanoPagina = Paginacion.TamanoPorDefecto
) : IRequest<Result<ResultadoPaginado<TransaccionDto>>>;

public class ListarTransaccionesHandler(ITransaccionConsultas repositorio)
    : IRequestHandler<ListarTransaccionesQuery, Result<ResultadoPaginado<TransaccionDto>>>
{
    public async Task<Result<ResultadoPaginado<TransaccionDto>>> Handle(ListarTransaccionesQuery query, CancellationToken cancellationToken)
    {
        if (Paginacion.Validar(query.Pagina, query.TamanoPagina) is { } error)
            return Result.Failure<ResultadoPaginado<TransaccionDto>>(error);

        var pagina = await repositorio.ListarAsync(
            query.Desde,
            query.Hasta,
            query.Categoria,
            query.Pagina,
            query.TamanoPagina,
            cancellationToken
        );

        return Result.Success(pagina);
    }
}
