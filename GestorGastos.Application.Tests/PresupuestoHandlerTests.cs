using GestorGastos.Application.Presupuestos.ActualizarLimite;
using GestorGastos.Application.Presupuestos.Crear;
using GestorGastos.Application.Presupuestos.Eliminar;
using GestorGastos.Application.Presupuestos.Listar;
using GestorGastos.Application.Presupuestos.ObtenerPorId;
using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

public class PresupuestoHandlerTests
{
    private readonly FakePresupuestoRepository _repositorio = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Presupuesto Sembrar(CategoriaTransaccion categoria = CategoriaTransaccion.Alimentacion, decimal limite = 400m)
    {
        var presupuesto = Presupuesto.Crear(categoria, limite).Value;
        _repositorio.Items.Add(presupuesto);
        return presupuesto;
    }

    private Task<GestorGastos.Domain.Common.Result<Presupuesto>> Crear(CategoriaTransaccion categoria, decimal limite) =>
        new CrearPresupuestoHandler(_repositorio, _repositorio, _unitOfWork).Handle(
            new CrearPresupuestoCommand(categoria, limite),
            CancellationToken.None
        );

    [Fact]
    public async Task Crear_ConDatosValidos_AgregaGuardaUnaVezYDevuelveElPresupuesto()
    {
        var resultado = await Crear(CategoriaTransaccion.Salud, 150m);

        Assert.True(resultado.IsSuccess);
        Assert.Same(resultado.Value, Assert.Single(_repositorio.Agregados));
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_SiYaExisteUnoParaLaCategoria_DevuelveConflictoSinGuardar()
    {
        Sembrar(CategoriaTransaccion.Salud, 100m);

        var resultado = await Crear(CategoriaTransaccion.Salud, 999m);

        Assert.Equal(PresupuestoErrors.YaExiste, resultado.Error);
        Assert.Empty(_repositorio.Agregados);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_ConUnaCategoriaDistinta_NoChocaConLosExistentes()
    {
        Sembrar(CategoriaTransaccion.Salud, 100m);

        var resultado = await Crear(CategoriaTransaccion.Transporte, 200m);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, _repositorio.Items.Count);
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_FallaSinAgregarNiGuardar()
    {
        var resultado = await Crear(CategoriaTransaccion.Salud, 0m);

        Assert.Equal(PresupuestoErrors.LimiteInvalido, resultado.Error);
        Assert.Empty(_repositorio.Agregados);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_ConCategoriaSalario_FallaAntesDeConsultarDuplicados()
    {
        var resultado = await Crear(CategoriaTransaccion.Salario, 100m);

        Assert.Equal(PresupuestoErrors.CategoriaNoPresupuestable, resultado.Error);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task ActualizarLimite_SiNoExiste_DevuelveNoEncontradoSinGuardar()
    {
        var handler = new ActualizarLimitePresupuestoHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new ActualizarLimitePresupuestoCommand(Guid.NewGuid(), 10m), CancellationToken.None);

        Assert.Equal(PresupuestoErrors.NoEncontrado, resultado.Error);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task ActualizarLimite_ConValorInvalido_FallaSinGuardarYSinModificar()
    {
        var presupuesto = Sembrar(limite: 400m);
        var handler = new ActualizarLimitePresupuestoHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new ActualizarLimitePresupuestoCommand(presupuesto.Id, -5m), CancellationToken.None);

        Assert.Equal(PresupuestoErrors.LimiteInvalido, resultado.Error);
        Assert.Equal(400m, presupuesto.LimiteMensual);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task ActualizarLimite_ConValorValido_ModificaYGuardaUnaVez()
    {
        var presupuesto = Sembrar(limite: 400m);
        var handler = new ActualizarLimitePresupuestoHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new ActualizarLimitePresupuestoCommand(presupuesto.Id, 550m), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(550m, presupuesto.LimiteMensual);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Eliminar_SiNoExiste_DevuelveNoEncontradoSinGuardar()
    {
        var handler = new EliminarPresupuestoHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new EliminarPresupuestoCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(PresupuestoErrors.NoEncontrado, resultado.Error);
        Assert.Empty(_repositorio.Eliminados);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Eliminar_SiExiste_LoQuitaYGuardaUnaVez()
    {
        var presupuesto = Sembrar();
        var handler = new EliminarPresupuestoHandler(_repositorio, _unitOfWork);

        var resultado = await handler.Handle(new EliminarPresupuestoCommand(presupuesto.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(presupuesto, Assert.Single(_repositorio.Eliminados));
        Assert.Empty(_repositorio.Items);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task ObtenerPorId_SiExiste_DevuelveElPresupuesto()
    {
        var presupuesto = Sembrar();

        var resultado = await new ObtenerPresupuestoPorIdHandler(_repositorio).Handle(
            new ObtenerPresupuestoPorIdQuery(presupuesto.Id),
            CancellationToken.None
        );

        Assert.Same(presupuesto, resultado.Value);
    }

    [Fact]
    public async Task ObtenerPorId_SiNoExiste_DevuelveNoEncontrado()
    {
        var resultado = await new ObtenerPresupuestoPorIdHandler(_repositorio).Handle(
            new ObtenerPresupuestoPorIdQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.Equal(PresupuestoErrors.NoEncontrado, resultado.Error);
    }

    [Fact]
    public async Task Listar_DevuelveTodosOrdenadosPorCategoria()
    {
        Sembrar(CategoriaTransaccion.Vivienda, 800m);
        Sembrar(CategoriaTransaccion.Alimentacion, 400m);

        var lista = await new ListarPresupuestosHandler(_repositorio).Handle(new ListarPresupuestosQuery(), CancellationToken.None);

        Assert.Equal([CategoriaTransaccion.Alimentacion, CategoriaTransaccion.Vivienda], lista.Select(p => p.Categoria));
    }
}
