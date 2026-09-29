using GestorGastos.Application.Abstractions;
using GestorGastos.Application.Transacciones;
using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<GestorGastosDbContext>(opt => opt.UseSqlite("Data Source=gastos.db"));
        services.AddScoped<ITransaccionRepository, TransaccionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
