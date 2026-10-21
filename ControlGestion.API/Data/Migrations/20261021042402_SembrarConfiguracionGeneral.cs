using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SembrarConfiguracionGeneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesGenerales",
                columns: new[] { "Clave", "Descripcion", "Valor" },
                values: new object[,]
                {
                    { "DiasRevisita", "Días entre una visita al cliente y la siguiente (7.1).", "15" },
                    { "DiasVencimientoCredito", "Días de plazo de una venta a crédito.", "30" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesGenerales",
                keyColumn: "Clave",
                keyValue: "DiasRevisita");

            migrationBuilder.DeleteData(
                table: "ConfiguracionesGenerales",
                keyColumn: "Clave",
                keyValue: "DiasVencimientoCredito");
        }
    }
}
