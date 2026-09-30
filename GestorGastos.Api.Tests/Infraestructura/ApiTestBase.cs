using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestorGastos.Api.Dtos;
using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Api.Tests.Infraestructura;

// Base de las pruebas de integración: una fábrica por clase y la tabla vacía antes de cada prueba.
public abstract class ApiTestBase(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Cliente { get; } = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GestorGastosDbContext>().Transacciones.ExecuteDeleteAsync();
    }

    public Task DisposeAsync()
    {
        Cliente.Dispose();
        return Task.CompletedTask;
    }

    protected static object Cuerpo(
        string descripcion = "Prueba",
        decimal monto = 10m,
        object? tipo = null,
        string? categoria = null,
        string fecha = "2026-09-15T10:30:00"
    ) =>
        new
        {
            descripcion,
            monto,
            tipo = tipo ?? "Egreso",
            categoria,
            fecha,
        };

    protected Task<HttpResponseMessage> Enviar(object cuerpo) => Cliente.PostAsJsonAsync("/transacciones", cuerpo, Json);

    // Crea una transacción y falla la prueba si la API no responde 201.
    protected async Task<TransaccionResponse> Crear(
        string descripcion = "Prueba",
        decimal monto = 10m,
        string tipo = "Egreso",
        string? categoria = null,
        string fecha = "2026-09-15T10:30:00"
    )
    {
        var respuesta = await Enviar(Cuerpo(descripcion, monto, tipo, categoria, fecha));
        Assert.Equal(System.Net.HttpStatusCode.Created, respuesta.StatusCode);

        return (await respuesta.Content.ReadFromJsonAsync<TransaccionResponse>(Json))!;
    }

    protected async Task<List<TransaccionResponse>> Listar(string consulta = "")
    {
        var lista = await Cliente.GetFromJsonAsync<List<TransaccionResponse>>($"/transacciones{consulta}", Json);
        return lista!;
    }

    protected async Task<decimal> Saldo()
    {
        using var documento = JsonDocument.Parse(await Cliente.GetStringAsync("/transacciones/saldo"));
        return documento.RootElement.GetProperty("saldo").GetDecimal();
    }

    protected static async Task<Dictionary<string, string[]>> ErroresDe(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }
}
