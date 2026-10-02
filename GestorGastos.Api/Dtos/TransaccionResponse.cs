using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Dtos;

// El contrato HTTP. Se mantiene aparte del DTO de Application: este añade SaldoBajo, que solo existe al crear.
public record TransaccionResponse(
    Guid Id,
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion Categoria,
    DateTime Fecha,
    bool SaldoBajo = false
)
{
    public static TransaccionResponse Desde(TransaccionDto transaccion, bool saldoBajo = false) =>
        new(
            transaccion.Id,
            transaccion.Descripcion,
            transaccion.Monto,
            transaccion.Tipo,
            transaccion.Categoria,
            transaccion.Fecha,
            saldoBajo
        );
}
