using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuitarTransicionesDelEstadoDePedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermiteTransicion",
                table: "EstadosPedido");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PermiteTransicion",
                table: "EstadosPedido",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 1,
                column: "PermiteTransicion",
                value: "[2,5]");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 2,
                column: "PermiteTransicion",
                value: "[3,5]");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 3,
                column: "PermiteTransicion",
                value: "[4,5]");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 4,
                column: "PermiteTransicion",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 5,
                column: "PermiteTransicion",
                value: "[]");
        }
    }
}
