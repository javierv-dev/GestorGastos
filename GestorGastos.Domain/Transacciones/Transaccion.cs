namespace GestorGastos.Domain.Transacciones;

public enum TipoTransaccion
{
    Ingreso,
    Egreso
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
    Otros
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

    public static Transaccion Crear(
        string descripcion, decimal monto, TipoTransaccion tipo,
        CategoriaTransaccion? categoria, DateTime fecha)
    {
        var transaccion = new Transaccion { Id = Guid.NewGuid() };
        transaccion.Aplicar(descripcion, monto, tipo, categoria, fecha);
        return transaccion;
    }

    public void Actualizar(
        string descripcion, decimal monto, TipoTransaccion tipo,
        CategoriaTransaccion? categoria, DateTime fecha) =>
        Aplicar(descripcion, monto, tipo, categoria, fecha);

    // Valida todo antes de asignar: la entidad nunca queda a medio cambiar.
    private void Aplicar(
        string descripcion, decimal monto, TipoTransaccion tipo,
        CategoriaTransaccion? categoria, DateTime fecha)
    {
        if (monto <= 0)
            throw new DomainException(nameof(Monto), "El monto debe ser mayor a cero.");
        if (!Enum.IsDefined(tipo))
            throw new DomainException(nameof(Tipo), "El tipo debe ser Ingreso o Egreso.");
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainException(nameof(Descripcion), "La descripción es obligatoria.");

        Descripcion = descripcion;
        Monto = monto;
        Tipo = tipo;
        Categoria = categoria ?? CategoriaTransaccion.Otros;
        Fecha = fecha;
    }
}
