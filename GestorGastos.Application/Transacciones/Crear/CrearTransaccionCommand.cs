using GestorGastos.Application.Abstractions;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;
using MediatR;

namespace GestorGastos.Application.Transacciones.Crear;

public record CrearTransaccionCommand(
    string Descripcion,
    decimal Monto,
    TipoTransaccion Tipo,
    CategoriaTransaccion? Categoria,
    DateTime Fecha
) : IRequest<Result<CrearTransaccionResultado>>;

public record CrearTransaccionResultado(Transaccion Transaccion, bool SaldoBajo);

public class CrearTransaccionHandler(ITransaccionRepository repositorio, IUnitOfWork unitOfWork)
    : IRequestHandler<CrearTransaccionCommand, Result<CrearTransaccionResultado>>
{
    private const decimal UmbralSaldoBajo = 100m;

    public async Task<Result<CrearTransaccionResultado>> Handle(CrearTransaccionCommand command, CancellationToken cancellationToken)
    {
        var creada = Transaccion.Crear(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);
        if (creada.IsFailure)
            return Result.Failure<CrearTransaccionResultado>(creada.Error);

        var transaccion = creada.Value;

        repositorio.Agregar(transaccion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        var saldoActual = await repositorio.ObtenerSaldoAsync(cancellationToken);

        var saldoBajo = transaccion switch
        {
            { Tipo: TipoTransaccion.Egreso } when saldoActual < UmbralSaldoBajo => true,
            _ => false,
        };

        return Result.Success(new CrearTransaccionResultado(transaccion, saldoBajo));
    }
}
