using System.Net;
using System.Text.Json;
using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Tests;

public class ConsultasApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetPorId_Existente_Responde200()
    {
        var creada = await Crear("Café");

        var respuesta = await Cliente.GetAsync($"/transacciones/{creada.Id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetPorId_Inexistente_Responde404()
    {
        var respuesta = await Cliente.GetAsync($"/transacciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetPorId_ConIdentificadorQueNoEsGuid_Responde404()
    {
        var respuesta = await Cliente.GetAsync("/transacciones/abc");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetLista_SinDatos_DevuelveListaVacia()
    {
        Assert.Empty(await Listar());
    }

    [Fact]
    public async Task GetLista_SinFiltros_DevuelveTodas()
    {
        await Crear("A");
        await Crear("B");

        Assert.Equal(2, (await Listar()).Count);
    }

    [Fact]
    public async Task GetLista_FiltraPorCategoria()
    {
        await Crear("Super", categoria: "Alimentacion");
        await Crear("Médico", categoria: "Salud");

        var lista = await Listar("?categoria=Alimentacion");

        Assert.Equal("Super", Assert.Single(lista).Descripcion);
    }

    [Fact]
    public async Task GetLista_FiltraPorDesde_IncluyendoElDiaIndicado()
    {
        await Crear("Agosto", fecha: "2026-08-10T00:00:00");
        await Crear("Septiembre", fecha: "2026-09-01T08:00:00");

        var lista = await Listar("?desde=2026-09-01");

        Assert.Equal("Septiembre", Assert.Single(lista).Descripcion);
    }

    [Fact]
    public async Task GetLista_FiltraPorHasta_IncluyendoTodoElDiaIndicado()
    {
        await Crear("Tarde del día 15", fecha: "2026-09-15T23:30:00");
        await Crear("Día 16", fecha: "2026-09-16T00:00:00");

        var lista = await Listar("?hasta=2026-09-15");

        Assert.Equal("Tarde del día 15", Assert.Single(lista).Descripcion);
    }

    [Fact]
    public async Task GetLista_CombinaRangoDeFechasYCategoria()
    {
        await Crear("Encaja", categoria: "Salud", fecha: "2026-09-10T00:00:00");
        await Crear("Otra categoría", categoria: "Vivienda", fecha: "2026-09-10T00:00:00");
        await Crear("Fuera de rango", categoria: "Salud", fecha: "2026-10-10T00:00:00");

        var lista = await Listar("?desde=2026-09-01&hasta=2026-09-30&categoria=Salud");

        Assert.Equal("Encaja", Assert.Single(lista).Descripcion);
    }

    [Fact]
    public async Task GetSaldo_SinDatos_EsCero()
    {
        Assert.Equal(0m, await Saldo());
    }

    [Fact]
    public async Task GetSaldo_EsIngresosMenosEgresos()
    {
        await Crear("Sueldo", 1000m, "Ingreso");
        await Crear("Alquiler", 250m, "Egreso");
        await Crear("Super", 50.5m, "Egreso");

        Assert.Equal(699.5m, await Saldo());
    }

    [Fact]
    public async Task GetResumen_AgrupaPorCategoriaYOrdenaPorElOrdenDelEnum()
    {
        await Crear("Sueldo", 1000m, "Ingreso", "Salario");
        await Crear("Super 1", 30m, "Egreso", "Alimentacion");
        await Crear("Super 2", 20m, "Egreso", "Alimentacion");
        await Crear("Reembolso", 5m, "Ingreso", "Alimentacion");
        await Crear("Alquiler", 400m, "Egreso", "Vivienda");

        using var documento = JsonDocument.Parse(await Cliente.GetStringAsync("/transacciones/resumen"));
        var filas = documento.RootElement.EnumerateArray().ToList();

        var categorias = filas.Select(f => f.GetProperty("categoria").GetString()).ToList();
        Assert.Equal(
            [nameof(CategoriaTransaccion.Alimentacion), nameof(CategoriaTransaccion.Vivienda), nameof(CategoriaTransaccion.Salario)],
            categorias
        );

        var alimentacion = filas[0];
        Assert.Equal(5m, alimentacion.GetProperty("totalIngresos").GetDecimal());
        Assert.Equal(50m, alimentacion.GetProperty("totalEgresos").GetDecimal());
        Assert.Equal(400m, filas[1].GetProperty("totalEgresos").GetDecimal());
        Assert.Equal(1000m, filas[2].GetProperty("totalIngresos").GetDecimal());
    }

    [Fact]
    public async Task GetResumen_SinDatos_DevuelveListaVacia()
    {
        using var documento = JsonDocument.Parse(await Cliente.GetStringAsync("/transacciones/resumen"));

        Assert.Equal(JsonValueKind.Array, documento.RootElement.ValueKind);
        Assert.Equal(0, documento.RootElement.GetArrayLength());
    }
}
