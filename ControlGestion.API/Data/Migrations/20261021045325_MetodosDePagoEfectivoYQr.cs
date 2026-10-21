using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetodosDePagoEfectivoYQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MetodoPago queda en Efectivo (0) y QR (1). Transferencia valía 1, así que ya es QR sin cambiar la fila.
            // Cheque (2), las tarjetas (3 y 4) y cualquier valor inválido que se haya guardado (H11) pasan a QR: son
            // pagos que no fueron en efectivo. Decisión de Cristopher (21/10/2026).
            migrationBuilder.Sql("UPDATE PagosCobranza SET MetodoPago = 1 WHERE MetodoPago NOT IN (0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se puede volver atrás: el método original de esos pagos no se guardó en ninguna parte.
        }
    }
}
