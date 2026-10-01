using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Presupuestos;
using GestorGastos.Application.Transacciones;
using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GestorGastosDbContext>(opt => opt.UseSqlite(connectionString));
        services.AddScoped<ITransaccionRepository, TransaccionRepository>();
        services.AddScoped<IPresupuestoRepository, PresupuestoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
