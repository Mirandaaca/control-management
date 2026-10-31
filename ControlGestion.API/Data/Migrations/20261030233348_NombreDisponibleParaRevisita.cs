using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class NombreDisponibleParaRevisita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Disponible para re-visita");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Disponible para Re-visita");
        }
    }
}
