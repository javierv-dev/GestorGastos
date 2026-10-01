using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Presupuestos;
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

public class CrearTransaccionHandler(
    ITransaccionRepository repositorio,
    ComprobadorDePresupuesto comprobador,
    IPoliticaDeSaldoBajo politicaSaldoBajo,
    IUnitOfWork unitOfWork
) : IRequestHandler<CrearTransaccionCommand, Result<CrearTransaccionResultado>>
{
    public async Task<Result<CrearTransaccionResultado>> Handle(CrearTransaccionCommand command, CancellationToken cancellationToken)
    {
        var creada = Transaccion.Crear(command.Descripcion, command.Monto, command.Tipo, command.Categoria, command.Fecha);
        if (creada.IsFailure)
            return Result.Failure<CrearTransaccionResultado>(creada.Error);

        var transaccion = creada.Value;

        // Un egreso que excede el presupuesto del mes se rechaza (409) antes de guardar nada.
        var presupuesto = await comprobador.ComprobarAsync(transaccion, excluirTransaccionId: null, cancellationToken);
        if (presupuesto.IsFailure)
            return Result.Failure<CrearTransaccionResultado>(presupuesto.Error);

        repositorio.Agregar(transaccion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        var saldoActual = await repositorio.ObtenerSaldoAsync(cancellationToken);

        // La regla de qué es "saldo bajo" vive en la política inyectada, no aquí.
        var saldoBajo = politicaSaldoBajo.EsSaldoBajo(transaccion, saldoActual);

        return Result.Success(new CrearTransaccionResultado(transaccion, saldoBajo));
    }
}
