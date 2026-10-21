using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DesactivarClientesConEstadoInactivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un solo mecanismo para desactivar: los clientes dados de baja pasan al estado Inactivo (id 4, sembrado)
            // antes de quitar la columna, así nadie reaparece como activo.
            migrationBuilder.Sql("UPDATE Clientes SET EstadoClienteId = 4 WHERE Activo = 0;");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Clientes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Clientes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE Clientes SET Activo = 0 WHERE EstadoClienteId = 4;");
        }
    }
}
