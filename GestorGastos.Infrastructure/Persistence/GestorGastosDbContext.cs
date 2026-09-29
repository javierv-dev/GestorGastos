using GestorGastos.Domain.Transacciones;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Infrastructure.Persistence;

public class GestorGastosDbContext(DbContextOptions<GestorGastosDbContext> options) : DbContext(options)
{
    public DbSet<Transaccion> Transacciones => Set<Transaccion>();
}
