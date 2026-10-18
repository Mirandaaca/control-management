using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrenciaEnClienteYRecalculoDeSaldos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VersionConcurrencia",
                table: "Clientes",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            // Los saldos guardados quedaron mal porque los pagos parciales no los bajaban (T4.04). Se recalculan con la
            // misma regla que usa CobranzaService: lo que se debe en las cobranzas Pendiente (0), Parcial (1) y Vencida (3).
            migrationBuilder.Sql("""
                UPDATE Clientes c
                SET c.SaldoPendiente = (
                    SELECT COALESCE(SUM(co.MontoTotal - co.MontoPagado), 0)
                    FROM Cobranzas co
                    WHERE co.ClienteId = c.Id AND co.Estado IN (0, 1, 3));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VersionConcurrencia",
                table: "Clientes");
        }
    }
}
