using System.Net;
using System.Net.Http.Json;
using GestorGastos.Api.Dtos;
using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Tests;

public class ActualizarYEliminarApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<HttpResponseMessage> Actualizar(Guid id, object cuerpo) => Cliente.PutAsJsonAsync($"/transacciones/{id}", cuerpo, Json);

    private async Task<TransaccionResponse> Leer(Guid id) =>
        (await Cliente.GetFromJsonAsync<TransaccionResponse>($"/transacciones/{id}", Json))!;

    [Fact]
    public async Task PutValido_Responde204YCambiaLosDatos()
    {
        var creada = await Crear("Original", 10m, "Egreso", "Salud");

        var respuesta = await Actualizar(creada.Id, Cuerpo("Nueva", 25m, "Ingreso", "Salario", "2026-10-01T00:00:00"));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var leida = await Leer(creada.Id);
        Assert.Equal("Nueva", leida.Descripcion);
        Assert.Equal(25m, leida.Monto);
        Assert.Equal(TipoTransaccion.Ingreso, leida.Tipo);
        Assert.Equal(CategoriaTransaccion.Salario, leida.Categoria);
        Assert.Equal(new DateTime(2026, 10, 1), leida.Fecha);
    }

    [Fact]
    public async Task PutInvalido_Responde400YNoModificaLaTransaccion()
    {
        var creada = await Crear("Original", 10m, "Egreso", "Salud");

        var respuesta = await Actualizar(creada.Id, Cuerpo("Nueva", -1m, "Ingreso", "Salario"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var leida = await Leer(creada.Id);
        Assert.Equal("Original", leida.Descripcion);
        Assert.Equal(10m, leida.Monto);
        Assert.Equal(CategoriaTransaccion.Salud, leida.Categoria);
    }

    [Fact]
    public async Task PutInexistente_Responde404()
    {
        var respuesta = await Actualizar(Guid.NewGuid(), Cuerpo());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_Existente_Responde204YLaTransaccionDesaparece()
    {
        var creada = await Crear();

        var respuesta = await Cliente.DeleteAsync($"/transacciones/{creada.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync($"/transacciones/{creada.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Repetido_Responde404LaSegundaVez()
    {
        var creada = await Crear();
        await Cliente.DeleteAsync($"/transacciones/{creada.Id}");

        var segunda = await Cliente.DeleteAsync($"/transacciones/{creada.Id}");

        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
    }

    [Fact]
    public async Task Delete_Inexistente_Responde404()
    {
        var respuesta = await Cliente.DeleteAsync($"/transacciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_ActualizaElSaldo()
    {
        await Crear("Sueldo", 500m, "Ingreso");
        var egreso = await Crear("Compra", 120m, "Egreso");
        Assert.Equal(380m, await Saldo());

        await Cliente.DeleteAsync($"/transacciones/{egreso.Id}");

        Assert.Equal(500m, await Saldo());
    }
}
