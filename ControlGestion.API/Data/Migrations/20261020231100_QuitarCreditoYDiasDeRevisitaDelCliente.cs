using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuitarCreditoYDiasDeRevisitaDelCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasRevisita",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TieneCredito",
                table: "Clientes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasRevisita",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LimiteCredito",
                table: "Clientes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "TieneCredito",
                table: "Clientes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
