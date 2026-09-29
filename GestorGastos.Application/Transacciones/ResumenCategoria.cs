using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Transacciones;

public record ResumenCategoria(CategoriaTransaccion Categoria, decimal TotalIngresos, decimal TotalEgresos);
