using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorregirResponsableDeCobranzas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hasta ahora la cobranza quedaba a nombre de quien confirmaba la HP. El responsable es quien la vendió.
            migrationBuilder.Sql("""
                UPDATE Cobranzas c
                JOIN HojasPedido h ON h.Id = c.HojaPedidoId
                SET c.PromotorId = h.PromotorId
                WHERE c.PromotorId <> h.PromotorId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay vuelta atrás: el responsable anterior no quedó guardado en ninguna parte.
        }
    }
}
