using GestorGastos.Application.Common;
using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones;
using GestorGastos.Application.Transacciones.Listar;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

public class PaginacionHandlerTests
{
    private static readonly DateTime Fecha = new(2026, 9, 29);

    private readonly FakeTransaccionRepository _repositorio = new();

    private Task<Result<ResultadoPaginado<TransaccionDto>>> Listar(int pagina, int tamanoPagina) =>
        new ListarTransaccionesHandler(_repositorio).Handle(
            new ListarTransaccionesQuery(null, null, null, pagina, tamanoPagina),
            CancellationToken.None
        );

    private void Sembrar(int cantidad)
    {
        for (var i = 1; i <= cantidad; i++)
            _repositorio.Items.Add(Transaccion.Crear($"T{i}", i, TipoTransaccion.Ingreso, null, Fecha).Value);
    }

    [Fact]
    public async Task Listar_PorDefecto_PideLaPrimeraPaginaDeVeinte()
    {
        await new ListarTransaccionesHandler(_repositorio).Handle(new ListarTransaccionesQuery(null, null, null), CancellationToken.None);

        Assert.Equal((1, 20), _repositorio.UltimaPaginacion);
    }

    [Fact]
    public async Task Listar_PasaLaPaginacionAlRepositorio()
    {
        await Listar(3, 7);

        Assert.Equal((3, 7), _repositorio.UltimaPaginacion);
    }

    [Fact]
    public async Task Listar_DevuelveLaPaginaPedidaYLosTotales()
    {
        Sembrar(5);

        var pagina = (await Listar(2, 2)).Value;

        Assert.Equal(["T3", "T4"], pagina.Items.Select(t => t.Descripcion));
        Assert.Equal(5, pagina.Total);
        Assert.Equal(3, pagina.TotalPaginas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Listar_ConPaginaMenorAUno_DevuelvePaginaInvalidaSinConsultar(int pagina)
    {
        var resultado = await Listar(pagina, 10);

        Assert.Equal(Paginacion.PaginaInvalida, resultado.Error);
        Assert.Equal(0, _repositorio.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task Listar_ConTamanoFueraDeRango_DevuelveTamanoInvalidoSinConsultar(int tamano)
    {
        var resultado = await Listar(1, tamano);

        Assert.Equal(Paginacion.TamanoPaginaInvalido, resultado.Error);
        Assert.Equal(0, _repositorio.Consultas);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Listar_ConTamanoEnLosLimites_EsValido(int tamano)
    {
        var resultado = await Listar(1, tamano);

        Assert.True(resultado.IsSuccess);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 20, 5)]
    public void TotalPaginas_RedondeaHaciaArriba(int total, int tamano, int esperado)
    {
        var pagina = new ResultadoPaginado<int>([], 1, tamano, total);

        Assert.Equal(esperado, pagina.TotalPaginas);
    }
}
