using GestorGastos.Domain.Presupuestos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorGastos.Infrastructure.Persistence.Configurations;

public class PresupuestoConfiguration : IEntityTypeConfiguration<Presupuesto>
{
    public void Configure(EntityTypeBuilder<Presupuesto> builder)
    {
        // Red de seguridad en la base de datos: nunca dos presupuestos para la misma categoría,
        // aunque dos peticiones simultáneas pasen la comprobación del handler.
        builder.HasIndex(p => p.Categoria).IsUnique();
    }
}
