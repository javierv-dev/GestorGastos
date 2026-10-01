using GestorGastos.Application.Presupuestos;
using GestorGastos.Domain.Transacciones;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        decimal umbralSaldoBajo = PoliticaDeUmbralFijo.UmbralPorDefecto
    )
    {
        // Registra todos los handlers de este ensamblado.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // La política de saldo bajo se elige aquí, en la raíz de composición: el umbral llega por configuración.
        services.AddSingleton<IPoliticaDeSaldoBajo>(new PoliticaDeUmbralFijo(umbralSaldoBajo));

        // El servicio de aplicación usa repositorios con alcance por petición, así que también lo tiene.
        services.AddScoped<ComprobadorDePresupuesto>();
        return services;
    }
}
