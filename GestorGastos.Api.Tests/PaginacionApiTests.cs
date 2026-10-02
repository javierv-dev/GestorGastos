using System.Net;
using System.Text.Json;
using GestorGastos.Api.Tests.Infraestructura;

namespace GestorGastos.Api.Tests;

public class PaginacionApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task Sembrar(int cantidad, string fecha = "2026-09-15T10:00:00")
    {
        for (var i = 1; i <= cantidad; i++)
            await Crear($"T{i}", fecha: fecha);
    }

    [Fact]
    public async Task SinParametros_DevuelveLaPrimeraPaginaDeVeinteConSusTotales()
    {
        await Sembrar(25);

        var pagina = await ListarPagina();

        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(20, pagina.TamanoPagina);
        Assert.Equal(20, pagina.Items.Count);
        Assert.Equal(25, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
    }

    [Fact]
    public async Task SinDatos_DevuelveUnaPaginaVaciaConTotalCero()
    {
        var pagina = await ListarPagina();

        Assert.Empty(pagina.Items);
        Assert.Equal(0, pagina.Total);
        Assert.Equal(0, pagina.TotalPaginas);
    }

    [Fact]
    public async Task ElCuerpoUsaLosNombresDocumentados()
    {
        await Crear();

        using var documento = JsonDocument.Parse(await Cliente.GetStringAsync("/transacciones"));
        var nombres = documento.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["items", "pagina", "tamanoPagina", "total", "totalPaginas"], nombres);
    }

    [Fact]
    public async Task ElUltimoTramoEsUnaPaginaIncompleta()
    {
        await Sembrar(5);

        var pagina = await ListarPagina("?pagina=3&tamanoPagina=2");

        Assert.Single(pagina.Items);
        Assert.Equal(3, pagina.Pagina);
        Assert.Equal(5, pagina.Total);
        Assert.Equal(3, pagina.TotalPaginas);
    }

    [Fact]
    public async Task RecorrerTodasLasPaginas_NoRepiteNiPierdeNada_AunConLaMismaFecha()
    {
        // Siete transacciones con la fecha idéntica: sin un desempate por Id el orden entre páginas no estaría garantizado.
        await Sembrar(7);

        var vistos = new List<Guid>();
        for (var p = 1; p <= 3; p++)
            vistos.AddRange((await ListarPagina($"?pagina={p}&tamanoPagina=3")).Items.Select(t => t.Id));

        Assert.Equal(7, vistos.Count);
        Assert.Equal(7, vistos.Distinct().Count());
    }

    [Fact]
    public async Task LosElementosVienenDeLaFechaMasRecienteALaMasAntigua()
    {
        await Crear("Antigua", fecha: "2026-01-10T00:00:00");
        await Crear("Reciente", fecha: "2026-09-10T00:00:00");
        await Crear("Intermedia", fecha: "2026-05-10T00:00:00");

        var descripciones = (await Listar()).Select(t => t.Descripcion);

        Assert.Equal(["Reciente", "Intermedia", "Antigua"], descripciones);
    }

    [Fact]
    public async Task PaginaMasAllaDelFinal_Responde200ConItemsVaciosYElTotalReal()
    {
        await Sembrar(3);

        var pagina = await ListarPagina("?pagina=99&tamanoPagina=2");

        Assert.Empty(pagina.Items);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
    }

    [Fact]
    public async Task PaginaEnorme_NoDesbordaYDevuelveItemsVacios()
    {
        await Sembrar(2);

        // (int.MaxValue - 1) * 100 desborda un int y daría un salto negativo: devolvería elementos en vez de una página vacía.
        var pagina = await ListarPagina($"?pagina={int.MaxValue}&tamanoPagina=100");

        Assert.Empty(pagina.Items);
        Assert.Equal(2, pagina.Total);
    }

    [Fact]
    public async Task ConLaMismaFecha_LosEmpatesSeDesempatanPorId()
    {
        await Sembrar(8);

        var ids = (await Listar()).Select(t => t.Id.ToString().ToUpperInvariant());

        // SQLite guarda el Guid como texto en mayúsculas y lo ordena de forma binaria (ordinal).
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
    }

    [Fact]
    public async Task LosFiltrosSeAplicanAntesDeContarYPaginar()
    {
        await Crear("Salud 1", categoria: "Salud");
        await Crear("Salud 2", categoria: "Salud");
        await Crear("Salud 3", categoria: "Salud");
        await Crear("Vivienda", categoria: "Vivienda");

        var pagina = await ListarPagina("?categoria=Salud&pagina=1&tamanoPagina=2");

        Assert.Equal(2, pagina.Items.Count);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
    }

    [Theory]
    [InlineData("pagina=0", "Pagina")]
    [InlineData("pagina=-1", "Pagina")]
    [InlineData("tamanoPagina=0", "TamanoPagina")]
    [InlineData("tamanoPagina=101", "TamanoPagina")]
    public async Task ParametrosFueraDeRango_Responden400IndicandoElCampo(string consulta, string campo)
    {
        var respuesta = await Cliente.GetAsync($"/transacciones?{consulta}");

        await EsperarEstado(respuesta, HttpStatusCode.BadRequest);
        Assert.Contains(campo, (await ErroresDe(respuesta)).Keys);
    }

    [Fact]
    public async Task TamanoMaximoPermitido_Responde200()
    {
        var respuesta = await Cliente.GetAsync("/transacciones?tamanoPagina=100");

        await EsperarEstado(respuesta, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("pagina=abc")]
    [InlineData("tamanoPagina=diez")]
    public async Task ParametrosQueNoSonNumeros_Responden400(string consulta)
    {
        var respuesta = await Cliente.GetAsync($"/transacciones?{consulta}");

        await EsperarEstado(respuesta, HttpStatusCode.BadRequest);
    }
}
