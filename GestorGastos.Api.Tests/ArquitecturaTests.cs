using System.Reflection;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Api.Tests;

// Automatiza la regla de dependencias del paso 1: cada capa solo mira hacia adentro.
public class ArquitecturaTests
{
    private static readonly Assembly Domain = typeof(Transaccion).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static List<string> Referencias(Assembly ensamblado) => ensamblado.GetReferencedAssemblies().Select(r => r.Name!).ToList();

    [Fact]
    public void Domain_NoDependeDeNingunaOtraCapaNiDeFrameworksDeInfraestructura()
    {
        var prohibidas = Referencias(Domain)
            .Where(r =>
                r.StartsWith("GestorGastos.", StringComparison.Ordinal)
                || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || r.StartsWith("MediatR", StringComparison.Ordinal)
            );

        Assert.Empty(prohibidas);
    }

    [Fact]
    public void Application_NoDependeDeInfrastructureNiDeApiNiDeEntityFramework()
    {
        var prohibidas = Referencias(Application)
            .Where(r =>
                r is "GestorGastos.Infrastructure" or "GestorGastos.Api"
                || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            );

        Assert.Empty(prohibidas);
    }

    [Fact]
    public void Infrastructure_NoDependeDeApi()
    {
        Assert.DoesNotContain("GestorGastos.Api", Referencias(Infrastructure));
    }

    [Fact]
    public void Api_NoUsaEntityFrameworkDirectamente()
    {
        Assert.DoesNotContain(Referencias(Api), r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
