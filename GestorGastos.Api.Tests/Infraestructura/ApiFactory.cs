using GestorGastos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GestorGastos.Api.Tests.Infraestructura;

// Levanta la API real con una base SQLite propia en un archivo temporal, creada con las migraciones de verdad.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string RutaBd { get; } = Path.Combine(Path.GetTempPath(), $"gestorgastos-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={RutaBd}");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<GestorGastosDbContext>().Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        SqliteConnection.ClearAllPools();
        foreach (var ruta in new[] { RutaBd, $"{RutaBd}-journal", $"{RutaBd}-wal", $"{RutaBd}-shm" })
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }
}
