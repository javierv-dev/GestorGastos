using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestorGastos.Api.Dtos;
using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Tests;

public class PresupuestosApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<HttpResponseMessage> Enviar(string categoria, decimal limite) =>
        Cliente.PostAsJsonAsync("/presupuestos", new { categoria, limiteMensual = limite }, Json);

    private async Task<PresupuestoResponse> CrearPresupuesto(string categoria = "Alimentacion", decimal limite = 400m)
    {
        var respuesta = await Enviar(categoria, limite);
        await EsperarEstado(respuesta, HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<PresupuestoResponse>(Json))!;
    }

    private Task<HttpResponseMessage> CambiarLimite(Guid id, decimal limite) =>
        Cliente.PutAsJsonAsync($"/presupuestos/{id}", new { limiteMensual = limite }, Json);

    [Fact]
    public async Task Post_Valido_Responde201ConLocationYElCuerpo()
    {
        var respuesta = await Enviar("Vivienda", 850m);

        await EsperarEstado(respuesta, HttpStatusCode.Created);
        var creado = (await respuesta.Content.ReadFromJsonAsync<PresupuestoResponse>(Json))!;
        Assert.Equal($"/presupuestos/{creado.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(CategoriaTransaccion.Vivienda, creado.Categoria);
        Assert.Equal(850m, creado.LimiteMensual);
    }

    [Fact]
    public async Task Post_ParaUnaCategoriaQueYaTienePresupuesto_Responde409()
    {
        await CrearPresupuesto("Salud", 100m);

        var respuesta = await Enviar("Salud", 500m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("Presupuesto.YaExiste", problema.RootElement.GetProperty("title").GetString());
        Assert.Single(await Cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/presupuestos", Json) ?? []);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Post_ConLimiteNoPositivo_Responde400ConElCampo(int limite)
    {
        var respuesta = await Enviar("Salud", limite);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(["El límite mensual debe ser mayor a cero."], (await ErroresDe(respuesta))["LimiteMensual"]);
    }

    [Fact]
    public async Task Post_ConCategoriaSalario_Responde400()
    {
        var respuesta = await Enviar("Salario", 100m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await ErroresDe(respuesta)).ContainsKey("Categoria"));
    }

    [Fact]
    public async Task Post_ConCategoriaQueNoExisteComoTexto_Responde400()
    {
        var respuesta = await Enviar("Inventada", 100m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Get_Lista_DevuelveLosPresupuestosOrdenadosPorCategoria()
    {
        await CrearPresupuesto("Vivienda", 800m);
        await CrearPresupuesto("Alimentacion", 400m);

        var lista = await Cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/presupuestos", Json);

        Assert.Equal([CategoriaTransaccion.Alimentacion, CategoriaTransaccion.Vivienda], lista!.Select(p => p.Categoria));
    }

    [Fact]
    public async Task Get_Lista_SinDatos_DevuelveListaVacia()
    {
        var lista = await Cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/presupuestos", Json);

        Assert.Empty(lista!);
    }

    [Fact]
    public async Task Get_PorId_Existente_Responde200()
    {
        var creado = await CrearPresupuesto();

        var leido = await Cliente.GetFromJsonAsync<PresupuestoResponse>($"/presupuestos/{creado.Id}", Json);

        Assert.Equal(creado, leido);
    }

    [Fact]
    public async Task Get_PorId_Inexistente_Responde404()
    {
        var respuesta = await Cliente.GetAsync($"/presupuestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_Valido_Responde204YCambiaElLimite()
    {
        var creado = await CrearPresupuesto(limite: 400m);

        var respuesta = await CambiarLimite(creado.Id, 650m);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var leido = await Cliente.GetFromJsonAsync<PresupuestoResponse>($"/presupuestos/{creado.Id}", Json);
        Assert.Equal(650m, leido!.LimiteMensual);
        Assert.Equal(creado.Categoria, leido.Categoria);
    }

    [Fact]
    public async Task Put_Invalido_Responde400YNoModifica()
    {
        var creado = await CrearPresupuesto(limite: 400m);

        var respuesta = await CambiarLimite(creado.Id, 0m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var leido = await Cliente.GetFromJsonAsync<PresupuestoResponse>($"/presupuestos/{creado.Id}", Json);
        Assert.Equal(400m, leido!.LimiteMensual);
    }

    [Fact]
    public async Task Put_Inexistente_Responde404()
    {
        var respuesta = await CambiarLimite(Guid.NewGuid(), 100m);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_Existente_Responde204Y_AlRepetirResponde404()
    {
        var creado = await CrearPresupuesto();

        var primera = await Cliente.DeleteAsync($"/presupuestos/{creado.Id}");
        var segunda = await Cliente.DeleteAsync($"/presupuestos/{creado.Id}");

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync($"/presupuestos/{creado.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_PermiteVolverACrearUnPresupuestoParaLaMismaCategoria()
    {
        var creado = await CrearPresupuesto("Salud", 100m);
        await Cliente.DeleteAsync($"/presupuestos/{creado.Id}");

        var nuevo = await Enviar("Salud", 200m);

        Assert.Equal(HttpStatusCode.Created, nuevo.StatusCode);
    }

    [Fact]
    public async Task Los_Presupuestos_NoAfectanALasTransacciones()
    {
        await CrearPresupuesto("Alimentacion", 400m);
        await Crear("Super", 50m, "Egreso", "Alimentacion");

        Assert.Single(await Listar());
        Assert.Equal(-50m, await Saldo());
    }
}
