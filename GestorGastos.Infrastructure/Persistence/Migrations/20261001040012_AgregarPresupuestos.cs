using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorGastos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPresupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Presupuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Categoria = table.Column<int>(type: "INTEGER", nullable: false),
                    LimiteMensual = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presupuestos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Categoria",
                table: "Presupuestos",
                column: "Categoria",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Presupuestos");
        }
    }
}
