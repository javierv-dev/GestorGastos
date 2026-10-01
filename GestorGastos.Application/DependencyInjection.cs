using GestorGastos.Application.Presupuestos;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registra todos los handlers de este ensamblado.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // El servicio de aplicación usa repositorios con alcance por petición, así que también lo tiene.
        services.AddScoped<ComprobadorDePresupuesto>();
        return services;
    }
}
