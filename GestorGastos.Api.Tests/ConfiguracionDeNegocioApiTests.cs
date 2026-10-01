using GestorGastos.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;

namespace GestorGastos.Api.Tests;

// La API arrancada con OTRO umbral de saldo bajo, solo por configuración (Negocio:UmbralSaldoBajo). Sin recompilar nada.
public sealed class ApiFactoryConUmbralDe500 : ApiFactory
{
    protected override void Ajustar(IWebHostBuilder builder) => builder.UseSetting("Negocio:UmbralSaldoBajo", "500");
}

public class ConfiguracionDeNegocioApiTests(ApiFactoryConUmbralDe500 factory)
    : ApiTestBase(factory),
        IClassFixture<ApiFactoryConUmbralDe500>
{
    [Fact]
    public async Task ConUmbralConfiguradoEn500_UnEgresoQueDeja300AvisaSaldoBajo()
    {
        await Crear("Sueldo", 400m, "Ingreso");

        var egreso = await Crear("Compra", 100m, "Egreso");

        // Con el umbral por defecto (100) este mismo egreso NO avisaría: ver CrearTransaccionApiTests.
        Assert.True(egreso.SaldoBajo);
    }

    [Fact]
    public async Task ConUmbralConfiguradoEn500_QuedarExactamenteEn500NoAvisa()
    {
        await Crear("Sueldo", 600m, "Ingreso");

        var egreso = await Crear("Compra", 100m, "Egreso");

        Assert.False(egreso.SaldoBajo);
    }

    [Fact]
    public async Task ConUmbralConfiguradoEn500_UnIngresoNuncaAvisa()
    {
        var ingreso = await Crear("Cobro", 10m, "Ingreso");

        Assert.False(ingreso.SaldoBajo);
    }
}
