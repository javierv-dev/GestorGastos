using GestorGastos.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorGastos.Api.Data;

public class GestorGastosDbContext(DbContextOptions<GestorGastosDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
}
