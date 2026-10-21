using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistroYDireccionDelPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Primero sin exigirla: los pedidos que ya existen reciben su valor antes de que pase a obligatoria.
            migrationBuilder.AddColumn<string>(
                name: "RegistradoPorId",
                table: "Pedidos",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DireccionEntrega",
                table: "Pedidos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Hasta ahora PromotorId era quien registró el pedido.
            migrationBuilder.Sql("UPDATE Pedidos SET RegistradoPorId = PromotorId;");

            migrationBuilder.AlterColumn<string>(
                name: "PromotorId",
                table: "Pedidos",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // El pedido pasa a la cartera de su cliente (7.5), y su dirección de entrega es la del cliente.
            migrationBuilder.Sql(@"
                UPDATE Pedidos p
                JOIN Clientes c ON c.Id = p.ClienteId
                SET p.PromotorId = c.PromotorId,
                    p.DireccionEntrega = LEFT(c.DireccionTexto, 300);");

            // El panel viejo mandaba 0001-01-01 cuando no se elegía la fecha: no es una fecha, es un vacío.
            migrationBuilder.Sql("UPDATE Pedidos SET FechaEntregaEstimada = NULL WHERE FechaEntregaEstimada < '1900-01-01';");

            migrationBuilder.AlterColumn<string>(
                name: "RegistradoPorId",
                table: "Pedidos",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_RegistradoPorId",
                table: "Pedidos",
                column: "RegistradoPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_AspNetUsers_RegistradoPorId",
                table: "Pedidos",
                column: "RegistradoPorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_AspNetUsers_RegistradoPorId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_RegistradoPorId",
                table: "Pedidos");

            // Vuelve a quedar a nombre de quien lo registró. Las fechas 0001-01-01 no se restauran: no eran fechas.
            migrationBuilder.Sql("UPDATE Pedidos SET PromotorId = RegistradoPorId;");

            migrationBuilder.DropColumn(
                name: "DireccionEntrega",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "RegistradoPorId",
                table: "Pedidos");

            migrationBuilder.AlterColumn<string>(
                name: "PromotorId",
                table: "Pedidos",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
