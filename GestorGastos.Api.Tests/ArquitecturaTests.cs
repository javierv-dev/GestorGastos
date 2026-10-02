using System.Reflection;
using System.Runtime.CompilerServices;
using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Presupuestos;
using GestorGastos.Application.Transacciones;
using GestorGastos.Domain.Presupuestos;
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

        Assert.Equal(["Agregar", "Eliminar", "ObtenerPorCategoriaAsync", "ObtenerPorIdAsync"], Metodos(typeof(IPresupuestoRepository)));
        Assert.Equal(["ListarAsync", "ObtenerPorIdAsync"], Metodos(typeof(IPresupuestoConsultas)));
    }

    private static readonly Type[] Entidades = [typeof(Transaccion), typeof(Presupuesto)];

    // Todos los tipos que se alcanzan desde uno: él mismo, sus argumentos genéricos y los tipos de sus propiedades (solo dentro de GestorGastos).
    private static IEnumerable<Type> TiposAlcanzables(Type tipo, HashSet<Type>? visitados = null)
    {
        visitados ??= [];
        if (!visitados.Add(tipo))
            yield break;

        yield return tipo;

        foreach (var interno in tipo.GenericTypeArguments.Concat(tipo.HasElementType ? [tipo.GetElementType()!] : []))
        foreach (var alcanzado in TiposAlcanzables(interno, visitados))
            yield return alcanzado;

        if (tipo.Namespace?.StartsWith("GestorGastos", StringComparison.Ordinal) != true)
            yield break;

        foreach (var propiedad in tipo.GetProperties())
        foreach (var alcanzado in TiposAlcanzables(propiedad.PropertyType, visitados))
            yield return alcanzado;
    }

    private static List<string> ExponenEntidades(Type tipo) =>
        TiposAlcanzables(tipo).Where(Entidades.Contains).Select(e => e.Name).Distinct().ToList();

    [Fact]
    public void LasRespuestasDeLosCasosDeUso_NoExponenEntidades()
    {
        var respuestas = Application
            .GetExportedTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "IRequestHandler`2")
            .Select(i => i.GetGenericArguments()[1])
            .Distinct()
            .ToList();

        Assert.NotEmpty(respuestas);
        var infractores = respuestas.Where(r => ExponenEntidades(r).Count > 0).Select(r => r.Name).ToList();
        Assert.Empty(infractores);
    }

    [Fact]
    public void LasConsultas_DevuelvenModelosDeLecturaYNoRecibenEntidades()
    {
        var infractores = new[] { typeof(ITransaccionConsultas), typeof(IPresupuestoConsultas) }
            .SelectMany(i => i.GetMethods())
            .Where(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType).Any(t => ExponenEntidades(t).Count > 0))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Empty(infractores);
    }

    [Fact]
    public void LaApi_NoUsaEntidadesEnNingunaFirma()
    {
        const BindingFlags Todos =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var tiposDeApi = Api.GetTypes()
            .Where(t =>
                !t.IsNested
                && !t.IsDefined(typeof(CompilerGeneratedAttribute), false)
                && t.Namespace is not null
                && (
                    t.Namespace.EndsWith(".Dtos", StringComparison.Ordinal)
                    || t.Namespace.EndsWith(".Endpoints", StringComparison.Ordinal)
                    || t.Namespace.EndsWith(".Extensions", StringComparison.Ordinal)
                )
            )
            .ToList();

        Assert.NotEmpty(tiposDeApi);

        var infractores = tiposDeApi
            .SelectMany(t =>
                t.GetMethods(Todos)
                    .Select(m =>
                        (Miembro: $"{t.Name}.{m.Name}", Tipos: m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
                    )
                    .Concat(
                        t.GetConstructors(Todos)
                            .Select(c => (Miembro: $"{t.Name}.ctor", Tipos: c.GetParameters().Select(p => p.ParameterType)))
                    )
                    .Concat(
                        t.GetProperties(Todos).Select(p => (Miembro: $"{t.Name}.{p.Name}", Tipos: new[] { p.PropertyType }.AsEnumerable()))
                    )
            )
            .Where(m => m.Tipos.Any(tipo => ExponenEntidades(tipo).Count > 0))
            .Select(m => m.Miembro)
            .ToList();

        Assert.Empty(infractores);
    }
}
