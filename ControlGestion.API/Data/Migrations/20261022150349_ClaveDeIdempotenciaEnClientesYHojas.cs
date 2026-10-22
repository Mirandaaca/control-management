using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClaveDeIdempotenciaEnClientesYHojas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "HojasPedido",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "Clientes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_ClaveIdempotencia",
                table: "HojasPedido",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ClaveIdempotencia",
                table: "Clientes",
                column: "ClaveIdempotencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HojasPedido_ClaveIdempotencia",
                table: "HojasPedido");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_ClaveIdempotencia",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "HojasPedido");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "Clientes");
        }
    }
}
