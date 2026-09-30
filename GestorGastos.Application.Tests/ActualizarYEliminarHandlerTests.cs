using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones.Actualizar;
using GestorGastos.Application.Transacciones.Eliminar;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

public class ActualizarYEliminarHandlerTests
{
    private static readonly DateTime Fecha = new(2026, 9, 29);

    private readonly FakeTransaccionRepository _repositorio = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Transaccion Sembrar()
    {
        var transaccion = Transaccion.Crear("Original", 10m, TipoTransaccion.Egreso, CategoriaTransaccion.Salud, Fecha).Value;
        _repositorio.Items.Add(transaccion);
        return transaccion;
    }

    private Task<GestorGastos.Domain.Common.Result> Actualizar(Guid id, decimal monto) =>
        new ActualizarTransaccionHandler(_repositorio, _unitOfWork).Handle(
            new ActualizarTransaccionCommand(id, "Nueva", monto, TipoTransaccion.Egreso, null, Fecha),
            CancellationToken.None
        );

    [Fact]
    public async Task Actualizar_SiNoExiste_DevuelveNoEncontradaSinGuardar()
    {
        var resultado = await Actualizar(Guid.NewGuid(), 20m);

        Assert.Equal(TransaccionErrors.NoEncontrada, resultado.Error);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Actualizar_ConDatosInvalidos_FallaSinGuardarYSinModificar()
    {
        var transaccion = Sembrar();

        var resultado = await Actualizar(transaccion.Id, -1m);

        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
        Assert.Equal(10m, transaccion.Monto);
        Assert.Equal("Original", transaccion.Descripcion);
    }

    [Fact]
    public async Task Actualizar_ConDatosValidos_ModificaYGuardaUnaVez()
    {
        var transaccion = Sembrar();

        var resultado = await Actualizar(transaccion.Id, 20m);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(20m, transaccion.Monto);
        Assert.Equal("Nueva", transaccion.Descripcion);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Eliminar_SiNoExiste_DevuelveNoEncontradaSinGuardar()
    {
        var handler = new EliminarTransaccionHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new EliminarTransaccionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TransaccionErrors.NoEncontrada, resultado.Error);
        Assert.Empty(_repositorio.Eliminadas);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Eliminar_SiExiste_LaQuitaYGuardaUnaVez()
    {
        var transaccion = Sembrar();
        var handler = new EliminarTransaccionHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new EliminarTransaccionCommand(transaccion.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(transaccion, Assert.Single(_repositorio.Eliminadas));
        Assert.Empty(_repositorio.Items);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }
}
