using GestorGastos.Api.Tests.Infraestructura;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;
using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Api.Tests;

public class BaseDeDatosApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ElIndiceUnico_RechazaDosPresupuestosParaLaMismaCategoria_AunqueElHandlerNoLoDetecte()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestorGastosDbContext>();
        db.Presupuestos.Add(Presupuesto.Crear(CategoriaTransaccion.Salud, 10m).Value);
        db.Presupuestos.Add(Presupuesto.Crear(CategoriaTransaccion.Salud, 20m).Value);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

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
