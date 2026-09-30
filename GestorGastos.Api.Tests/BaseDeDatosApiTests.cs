using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Api.Tests;

public class BaseDeDatosApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public void LaCadenaDeConexionConfigurada_CreaLaBaseEnLaRutaDeLaPrueba()
    {
        Assert.True(File.Exists(Factory.RutaBd));
    }

    [Fact]
    public async Task LasMigracionesEstanTodasAplicadas()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestorGastosDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public void ElModeloCoincideConLasMigraciones_NoHayCambiosSinMigrar()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestorGastosDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
