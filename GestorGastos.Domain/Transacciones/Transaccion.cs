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
    public Guid Id { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public TipoTransaccion Tipo { get; set; }
    public CategoriaTransaccion Categoria { get; set; }
    public DateTime Fecha { get; set; }
}
