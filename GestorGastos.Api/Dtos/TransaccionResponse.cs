using GestorGastos.Api.Models;

namespace GestorGastos.Api.Dtos;

public record TransaccionResponse(
    Guid Id,
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion Categoria,
    DateTime Fecha,
    bool SaldoBajo = false)
{
    public static TransaccionResponse Desde(Transaccion transaccion, bool saldoBajo = false) =>
        new(transaccion.Id, transaccion.Descripcion, transaccion.Monto, transaccion.Tipo,
            transaccion.Categoria, transaccion.Fecha, saldoBajo);
}
