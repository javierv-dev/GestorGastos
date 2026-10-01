using GestorGastos.Domain.Common;

namespace GestorGastos.Domain.Presupuestos;

public static class PresupuestoErrors
{
    public static readonly ErrorNegocio LimiteInvalido = ErrorNegocio.Validacion(
        "LimiteMensual",
        "Presupuesto.LimiteInvalido",
        "El límite mensual debe ser mayor a cero."
    );

    public static readonly ErrorNegocio CategoriaInvalida = ErrorNegocio.Validacion(
        "Categoria",
        "Presupuesto.CategoriaInvalida",
        "La categoría no es válida."
    );

    public static readonly ErrorNegocio CategoriaNoPresupuestable = ErrorNegocio.Validacion(
        "Categoria",
        "Presupuesto.CategoriaNoPresupuestable",
        "No se puede presupuestar la categoría Salario porque es una fuente de ingresos."
    );

    public static readonly ErrorNegocio NoEncontrado = ErrorNegocio.NoEncontrado("Presupuesto.NoEncontrado", "El presupuesto no existe.");

    public static readonly ErrorNegocio YaExiste = ErrorNegocio.Conflicto(
        "Presupuesto.YaExiste",
        "Ya existe un presupuesto para esa categoría."
    );
}
