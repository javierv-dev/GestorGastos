using System.Globalization;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Domain.Presupuestos;

public static class PresupuestoErrors
{
    // A diferencia de los demás, este error lleva datos en el mensaje, así que es un método y no un campo.
    public static ErrorNegocio Excedido(CategoriaTransaccion categoria, decimal limiteMensual, decimal gastadoEnElMes)
    {
        var disponible = Math.Max(0m, limiteMensual - gastadoEnElMes);

        return ErrorNegocio.Conflicto(
            "Presupuesto.Excedido",
            string.Create(
                CultureInfo.InvariantCulture,
                $"El egreso excede el presupuesto mensual de {categoria}: límite {limiteMensual:0.00}, ya gastado {gastadoEnElMes:0.00}, disponible {disponible:0.00}."
            )
        );
    }

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
