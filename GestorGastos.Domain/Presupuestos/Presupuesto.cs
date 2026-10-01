using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Presupuestos;

// Límite de gasto mensual para una categoría. Hay como máximo uno por categoría.
public class Presupuesto
{
    // Constructor privado: solo EF Core lo usa para materializar la entidad.
    private Presupuesto() { }

    public Guid Id { get; private set; }
    public CategoriaTransaccion Categoria { get; private set; }
    public decimal LimiteMensual { get; private set; }

    public static Result<Presupuesto> Crear(CategoriaTransaccion categoria, decimal limiteMensual)
    {
        if (!Enum.IsDefined(categoria))
            return Result.Failure<Presupuesto>(PresupuestoErrors.CategoriaInvalida);
        if (categoria == CategoriaTransaccion.Salario)
            return Result.Failure<Presupuesto>(PresupuestoErrors.CategoriaNoPresupuestable);
        if (limiteMensual <= 0)
            return Result.Failure<Presupuesto>(PresupuestoErrors.LimiteInvalido);

        return Result.Success(
            new Presupuesto
            {
                Id = Guid.NewGuid(),
                Categoria = categoria,
                LimiteMensual = limiteMensual,
            }
        );
    }

    public Result CambiarLimite(decimal nuevoLimite)
    {
        if (nuevoLimite <= 0)
            return Result.Failure(PresupuestoErrors.LimiteInvalido);

        LimiteMensual = nuevoLimite;
        return Result.Success();
    }
}
