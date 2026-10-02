using System.Reflection;
using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Presupuestos;
using GestorGastos.Application.Transacciones;
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

    // Handlers públicos de Application.Transacciones cuyo request termina en "Query" (se detecta por nombre, sin referenciar MediatR).
    private static List<Type> HandlersDeConsultaDeTransacciones() =>
        Application
            .GetExportedTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && t.Namespace is not null
                && t.Namespace.StartsWith("GestorGastos.Application.Transacciones", StringComparison.Ordinal)
                && t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition().Name == "IRequestHandler`2"
                        && i.GetGenericArguments()[0].Name.EndsWith("Query", StringComparison.Ordinal)
                    )
            )
            .ToList();

    [Fact]
    public void HandlersDeConsultaDeTransacciones_ExistenParaQueLaReglaNoSeaVacua()
    {
        Assert.NotEmpty(HandlersDeConsultaDeTransacciones());
    }

    [Fact]
    public void HandlersDeConsultaDeTransacciones_NoDependenDeInterfacesDeEscritura()
    {
        var prohibidos = new[] { typeof(ITransaccionRepository), typeof(IUnitOfWork) };

        var infractores = HandlersDeConsultaDeTransacciones()
            .SelectMany(h =>
                h.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Where(p => prohibidos.Contains(p.ParameterType))
                    .Select(p => $"{h.Name}({p.ParameterType.Name})")
            )
            .ToList();

        Assert.Empty(infractores);
    }

    [Fact]
    public void InterfacesDeTransacciones_DeclaranExactamenteLosMetodosEsperados()
    {
        static string[] Metodos(Type t) => t.GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Agregar", "Eliminar", "ObtenerPorIdAsync"], Metodos(typeof(ITransaccionRepository)));
        Assert.Equal(
            ["ListarAsync", "ObtenerEgresosDelMesAsync", "ObtenerPorIdAsync", "ObtenerResumenPorCategoriaAsync", "ObtenerSaldoAsync"],
            Metodos(typeof(ITransaccionConsultas))
        );
    }

    // Handlers públicos de Application.Presupuestos cuyo request termina en "Query".
    private static List<Type> HandlersDeConsultaDePresupuestos() =>
        Application
            .GetExportedTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && t.Namespace is not null
                && t.Namespace.StartsWith("GestorGastos.Application.Presupuestos", StringComparison.Ordinal)
                && t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition().Name == "IRequestHandler`2"
                        && i.GetGenericArguments()[0].Name.EndsWith("Query", StringComparison.Ordinal)
                    )
            )
            .ToList();

    [Fact]
    public void HandlersDeConsultaDePresupuestos_ExistenParaQueLaReglaNoSeaVacua()
    {
        Assert.NotEmpty(HandlersDeConsultaDePresupuestos());
    }

    [Fact]
    public void HandlersDeConsultaDePresupuestos_NoDependenDeInterfacesDeEscritura()
    {
        var prohibidos = new[] { typeof(IPresupuestoRepository), typeof(IUnitOfWork) };

        var infractores = HandlersDeConsultaDePresupuestos()
            .SelectMany(h =>
                h.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Where(p => prohibidos.Contains(p.ParameterType))
                    .Select(p => $"{h.Name}({p.ParameterType.Name})")
            )
            .ToList();

        Assert.Empty(infractores);
    }

    [Fact]
    public void InterfacesDePresupuestos_DeclaranExactamenteLosMetodosEsperados()
    {
        static string[] Metodos(Type t) => t.GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Agregar", "Eliminar", "ObtenerPorIdAsync"], Metodos(typeof(IPresupuestoRepository)));
        Assert.Equal(["ListarAsync", "ObtenerPorCategoriaAsync", "ObtenerPorIdAsync"], Metodos(typeof(IPresupuestoConsultas)));
    }
}
