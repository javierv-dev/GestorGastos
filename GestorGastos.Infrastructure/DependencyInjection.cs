using GestorGastos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorGastos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<GestorGastosDbContext>(opt => opt.UseSqlite("Data Source=gastos.db"));
        return services;
    }
}
