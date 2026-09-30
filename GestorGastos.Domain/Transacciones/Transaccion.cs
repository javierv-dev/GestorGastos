using GestorGastos.Domain.Common;

namespace GestorGastos.Domain.Transacciones;

public enum TipoTransaccion
{
    Ingreso,
    Egreso,
}

public enum CategoriaTransaccion
{
    Alimentacion,
    Transporte,
    Vivienda,
    Servicios,
    Salud,
    Entretenimiento,
    Educacion,
    Salario,
    Otros,
}

public class Transaccion
{
    // Constructor privado: solo EF Core lo usa para materializar la entidad.
    private Transaccion() { }

    public Guid Id { get; private set; }
    public string Descripcion { get; private set; } = string.Empty;
    public decimal Monto { get; private set; }
    public TipoTransaccion Tipo { get; private set; }
    public CategoriaTransaccion Categoria { get; private set; }
    public DateTime Fecha { get; private set; }

    public static Result<Transaccion> Crear(
        string descripcion,
        decimal monto,
        TipoTransaccion tipo,
        CategoriaTransaccion? categoria,
        DateTime fecha
    )
    {
        var transaccion = new Transaccion { Id = Guid.NewGuid() };
        var resultado = transaccion.Aplicar(descripcion, monto, tipo, categoria, fecha);

        return resultado.IsSuccess ? Result.Success(transaccion) : Result.Failure<Transaccion>(resultado.Error);
    }

    public Result Actualizar(string descripcion, decimal monto, TipoTransaccion tipo, CategoriaTransaccion? categoria, DateTime fecha) =>
        Aplicar(descripcion, monto, tipo, categoria, fecha);

    // Valida todo antes de asignar: la entidad nunca queda a medio cambiar.
    private Result Aplicar(string descripcion, decimal monto, TipoTransaccion tipo, CategoriaTransaccion? categoria, DateTime fecha)
    {
        if (monto <= 0)
            return Result.Failure(TransaccionErrors.MontoInvalido);
        if (!Enum.IsDefined(tipo))
            return Result.Failure(TransaccionErrors.TipoInvalido);
        if (string.IsNullOrWhiteSpace(descripcion))
            return Result.Failure(TransaccionErrors.DescripcionObligatoria);

        Descripcion = descripcion;
        Monto = monto;
        Tipo = tipo;
        Categoria = categoria ?? CategoriaTransaccion.Otros;
        Fecha = fecha;
        return Result.Success();
    }
}
