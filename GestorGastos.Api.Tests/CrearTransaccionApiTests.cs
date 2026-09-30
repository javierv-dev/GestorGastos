using System.Net;
using System.Net.Http.Json;
using GestorGastos.Api.Dtos;
using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Tests;

public class CrearTransaccionApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task PostValido_Responde201ConLocationYElCuerpoCompleto()
    {
        var respuesta = await Enviar(Cuerpo("Sueldo", 1000m, "Ingreso", "Salario", "2026-08-10T00:00:00"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await respuesta.Content.ReadFromJsonAsync<TransaccionResponse>(Json))!;
        Assert.Equal($"/transacciones/{creada.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal("Sueldo", creada.Descripcion);
        Assert.Equal(1000m, creada.Monto);
        Assert.Equal(TipoTransaccion.Ingreso, creada.Tipo);
        Assert.Equal(CategoriaTransaccion.Salario, creada.Categoria);
        Assert.Equal(new DateTime(2026, 8, 10), creada.Fecha);
        Assert.False(creada.SaldoBajo);
    }

    [Fact]
    public async Task PostSinCategoria_UsaOtros()
    {
        var creada = await Crear(categoria: null);

        Assert.Equal(CategoriaTransaccion.Otros, creada.Categoria);
    }

    [Fact]
    public async Task PostValido_QuedaGuardadoYSeLeePorId()
    {
        var creada = await Crear("Café", 3.5m);

        var leida = await Cliente.GetFromJsonAsync<TransaccionResponse>($"/transacciones/{creada.Id}", Json);

        Assert.Equal("Café", leida!.Descripcion);
        Assert.Equal(3.5m, leida.Monto);
    }

    [Fact]
    public async Task PostEgresoQueDejaElSaldoBajo_AvisaSaldoBajo()
    {
        await Crear("Sueldo", 150m, "Ingreso");

        var egreso = await Crear("Alquiler", 100m, "Egreso");

        Assert.True(egreso.SaldoBajo);
    }

    [Fact]
    public async Task PostEgresoQueDejaExactamente100_NoAvisaSaldoBajo()
    {
        await Crear("Sueldo", 200m, "Ingreso");

        var egreso = await Crear("Alquiler", 100m, "Egreso");

        Assert.False(egreso.SaldoBajo);
    }

    [Theory]
    [InlineData(0, "Monto", "El monto debe ser mayor a cero.")]
    [InlineData(-5, "Monto", "El monto debe ser mayor a cero.")]
    public async Task PostConMontoInvalido_Responde400ConElErrorDelCampo(int monto, string campo, string mensaje)
    {
        var respuesta = await Enviar(Cuerpo(monto: monto));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal([mensaje], (await ErroresDe(respuesta))[campo]);
    }

    [Fact]
    public async Task PostConDescripcionVacia_Responde400()
    {
        var respuesta = await Enviar(Cuerpo(descripcion: " "));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(["La descripción es obligatoria."], (await ErroresDe(respuesta))["Descripcion"]);
    }

    [Fact]
    public async Task PostConTipoNumericoFueraDelEnum_Responde400()
    {
        var respuesta = await Enviar(Cuerpo(tipo: 99));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(["El tipo debe ser Ingreso o Egreso."], (await ErroresDe(respuesta))["Tipo"]);
    }

    [Fact]
    public async Task PostConTipoQueNoExisteComoTexto_Responde400()
    {
        var respuesta = await Enviar(Cuerpo(tipo: "Otro"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task PostInvalido_NoGuardaNada()
    {
        await Enviar(Cuerpo(monto: 0));

        Assert.Empty(await Listar());
    }
}
