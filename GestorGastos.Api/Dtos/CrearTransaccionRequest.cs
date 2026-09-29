using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Dtos;

public record CrearTransaccionRequest(
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion? Categoria,
    DateTime Fecha
);
