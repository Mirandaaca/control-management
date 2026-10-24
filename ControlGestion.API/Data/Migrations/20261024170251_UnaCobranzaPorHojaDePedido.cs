using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnaCobranzaPorHojaDePedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Una HP tiene a lo sumo una cobranza (7.3.5, paso 3.4). MySQL no deja borrar el índice que usa una FK: la FK sale
            // antes y vuelve después del índice único, como en EntregaDelPedido.
            migrationBuilder.DropForeignKey(
                name: "FK_Cobranzas_HojasPedido_HojaPedidoId",
                table: "Cobranzas");

            migrationBuilder.DropIndex(
                name: "IX_Cobranzas_HojaPedidoId",
                table: "Cobranzas");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_HojaPedidoId",
                table: "Cobranzas",
                column: "HojaPedidoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cobranzas_HojasPedido_HojaPedidoId",
                table: "Cobranzas",
                column: "HojaPedidoId",
                principalTable: "HojasPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cobranzas_HojasPedido_HojaPedidoId",
                table: "Cobranzas");

            migrationBuilder.DropIndex(
                name: "IX_Cobranzas_HojaPedidoId",
                table: "Cobranzas");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_HojaPedidoId",
                table: "Cobranzas",
                column: "HojaPedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cobranzas_HojasPedido_HojaPedidoId",
                table: "Cobranzas",
                column: "HojaPedidoId",
                principalTable: "HojasPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
