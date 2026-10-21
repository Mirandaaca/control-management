using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntregaDelPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL no deja borrar el índice que usa una FK: la FK sale antes y vuelve después del índice único.
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_HojasPedido_HojaPedidoId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_HojaPedidoId",
                table: "Pedidos");

            migrationBuilder.AddColumn<string>(
                name: "RecibidoPor",
                table: "Pedidos",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_HojaPedidoId",
                table: "Pedidos",
                column: "HojaPedidoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_HojasPedido_HojaPedidoId",
                table: "Pedidos",
                column: "HojaPedidoId",
                principalTable: "HojasPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_HojasPedido_HojaPedidoId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_HojaPedidoId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "RecibidoPor",
                table: "Pedidos");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_HojaPedidoId",
                table: "Pedidos",
                column: "HojaPedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_HojasPedido_HojaPedidoId",
                table: "Pedidos",
                column: "HojaPedidoId",
                principalTable: "HojasPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
