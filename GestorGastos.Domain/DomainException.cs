namespace GestorGastos.Domain;

public class DomainException(string campo, string mensaje) : Exception(mensaje)
{
    public string Campo { get; } = campo;
}
