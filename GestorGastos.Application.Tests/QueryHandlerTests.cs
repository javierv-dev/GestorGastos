using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones;
using GestorGastos.Application.Transacciones.Listar;
using GestorGastos.Application.Transacciones.ObtenerPorId;
using GestorGastos.Application.Transacciones.ObtenerResumen;
using GestorGastos.Application.Transacciones.ObtenerSaldo;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

public class QueryHandlerTests
{
    private static readonly DateTime Fecha = new(2026, 9, 29);

    private readonly FakeTransaccionRepository _repositorio = new();

    [Fact]
    public async Task ObtenerPorId_SiExiste_DevuelveLaTransaccion()
    {
        var transaccion = Transaccion.Crear("A", 5m, TipoTransaccion.Ingreso, null, Fecha).Value;
        _repositorio.Items.Add(transaccion);

        var resultado = await new ObtenerTransaccionPorIdHandler(_repositorio).Handle(
            new ObtenerTransaccionPorIdQuery(transaccion.Id),
            CancellationToken.None
        );

        Assert.Equal(TransaccionDto.Desde(transaccion), resultado.Value);
    }

    [Fact]
    public async Task ObtenerPorId_SiNoExiste_DevuelveNoEncontrada()
    {
        var resultado = await new ObtenerTransaccionPorIdHandler(_repositorio).Handle(
            new ObtenerTransaccionPorIdQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.Equal(TransaccionErrors.NoEncontrada, resultado.Error);
    }

    [Fact]
    public async Task Listar_PasaLosFiltrosAlRepositorio()
    {
        var desde = new DateTime(2026, 1, 1);
        var hasta = new DateTime(2026, 12, 31);

        await new ListarTransaccionesHandler(_repositorio).Handle(
            new ListarTransaccionesQuery(desde, hasta, CategoriaTransaccion.Salud),
            CancellationToken.None
        );

        Assert.Equal((desde, hasta, (CategoriaTransaccion?)CategoriaTransaccion.Salud), _repositorio.UltimoFiltro);
    }

    [Fact]
    public async Task ObtenerSaldo_DevuelveIngresosMenosEgresos()
    {
        _repositorio.Items.Add(Transaccion.Crear("I", 300m, TipoTransaccion.Ingreso, null, Fecha).Value);
        _repositorio.Items.Add(Transaccion.Crear("E", 120m, TipoTransaccion.Egreso, null, Fecha).Value);

        var saldo = await new ObtenerSaldoHandler(_repositorio).Handle(new ObtenerSaldoQuery(), CancellationToken.None);

        Assert.Equal(180m, saldo);
    }

    [Fact]
    public async Task ObtenerResumen_DevuelveLoQueEntregaElRepositorio()
    {
        _repositorio.Resumen = [new ResumenCategoria(CategoriaTransaccion.Salud, 0m, 40m)];

        var resumen = await new ObtenerResumenHandler(_repositorio).Handle(new ObtenerResumenQuery(), CancellationToken.None);

        Assert.Equal(new ResumenCategoria(CategoriaTransaccion.Salud, 0m, 40m), Assert.Single(resumen));
    }
}
