using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrenciaCobranzaYPagoIdempotente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveIdempotencia",
                table: "PagosCobranza",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "VersionConcurrencia",
                table: "Cobranzas",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.CreateIndex(
                name: "IX_PagosCobranza_ClaveIdempotencia",
                table: "PagosCobranza",
                column: "ClaveIdempotencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PagosCobranza_ClaveIdempotencia",
                table: "PagosCobranza");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "PagosCobranza");

            migrationBuilder.DropColumn(
                name: "VersionConcurrencia",
                table: "Cobranzas");
        }
    }
}
