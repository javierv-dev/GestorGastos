using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Listar;

public record ListarTransaccionesQuery(DateTime? Desde, DateTime? Hasta, CategoriaTransaccion? Categoria)
    : IRequest<IReadOnlyList<Transaccion>>;

public class ListarTransaccionesHandler(ITransaccionRepository repositorio)
    : IRequestHandler<ListarTransaccionesQuery, IReadOnlyList<Transaccion>>
{
    public Task<IReadOnlyList<Transaccion>> Handle(ListarTransaccionesQuery query, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(query.Desde, query.Hasta, query.Categoria, cancellationToken);
}
