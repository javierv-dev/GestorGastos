using System.Collections.Concurrent;
using System.Net.Http.Json;
using GestorGastos.Api.Tests.Infraestructura;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Api.Tests;

// Anota el token que recibe cada solicitud de MediatR. Un token "default" no es cancelable;
// el de la petición HTTP (RequestAborted) sí lo es, así que distingue si el endpoint lo pasó.
public sealed class RegistroDeTokens
{
    public ConcurrentBag<(string Solicitud, bool Cancelable)> Vistos { get; } = [];
}

public sealed class RegistrarToken<TRequest, TResponse>(RegistroDeTokens registro) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        registro.Vistos.Add((typeof(TRequest).Name, cancellationToken.CanBeCanceled));
        return next(cancellationToken);
    }
}

public sealed class ApiFactoryQueRegistraTokens : ApiFactory
{
    public RegistroDeTokens Registro { get; } = new();

    protected override void Ajustar(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Registro);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RegistrarToken<,>));
        });
}

public class CancelacionApiTests(ApiFactoryQueRegistraTokens factory) : ApiTestBase(factory), IClassFixture<ApiFactoryQueRegistraTokens>
{
    [Fact]
    public async Task TodosLosEndpoints_PasanElTokenDeLaPeticionAlHandler()
    {
        var registro = factory.Registro;
        registro.Vistos.Clear();

        // Transacciones: los siete endpoints.
        var creada = await Crear(categoria: "Alimentacion");
        await Listar();
        await Saldo();
        await Cliente.GetAsync("/transacciones/resumen");
        await Cliente.GetAsync($"/transacciones/{creada.Id}");
        await Cliente.PutAsJsonAsync($"/transacciones/{creada.Id}", Cuerpo(descripcion: "Editada"), Json);
        await Cliente.DeleteAsync($"/transacciones/{creada.Id}");

        // Presupuestos: los cinco endpoints.
        var respuesta = await Cliente.PostAsJsonAsync("/presupuestos", new { categoria = "Salud", limiteMensual = 100m }, Json);
        var presupuesto = await respuesta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var id = presupuesto.GetProperty("id").GetGuid();
        await Cliente.GetAsync("/presupuestos");
        await Cliente.GetAsync($"/presupuestos/{id}");
        await Cliente.PutAsJsonAsync($"/presupuestos/{id}", new { limiteMensual = 200m }, Json);
        await Cliente.DeleteAsync($"/presupuestos/{id}");

        Assert.Equal(12, registro.Vistos.Count);
        Assert.Empty(registro.Vistos.Where(v => !v.Cancelable).Select(v => v.Solicitud));
    }
}
