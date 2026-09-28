using GestorGastos.Api.Models;

namespace GestorGastos.Api.Dtos;

public record CrearTransaccionRequest(
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion? Categoria,
    DateTime Fecha);
