using System.Linq.Expressions;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

// Modelo de lectura: lo que Application expone de una transacción. La entidad nunca sale de esta capa.
public sealed record TransaccionDto(
    Guid Id,
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion Categoria,
    DateTime Fecha
)
{
    // Única definición del mapeo. Las consultas la pasan a Select para que la base de datos devuelva solo estas columnas...
    public static readonly Expression<Func<Transaccion, TransaccionDto>> Proyeccion = t => new TransaccionDto(
        t.Id,
        t.Descripcion,
        t.Monto,
        t.Tipo,
        t.Categoria,
        t.Fecha
    );

    // ...y la misma expresión, compilada una vez, sirve para convertir una entidad que ya está en memoria.
    private static readonly Func<Transaccion, TransaccionDto> Mapeador = Proyeccion.Compile();

    public static TransaccionDto Desde(Transaccion transaccion) => Mapeador(transaccion);
}
