using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClaveDeIdempotenciaEnPedidosYEventos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "Pedidos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "Eventos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClaveIdempotencia",
                table: "Pedidos",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_ClaveIdempotencia",
                table: "Eventos",
                column: "ClaveIdempotencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_ClaveIdempotencia",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Eventos_ClaveIdempotencia",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "Eventos");
        }
    }
}
