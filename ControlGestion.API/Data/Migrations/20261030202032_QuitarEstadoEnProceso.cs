using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <summary>
    /// Tarea 27: los pedidos pasan de Pendiente a En ruta, sin "En proceso" (id 2). Antes de borrar la fila del catálogo
    /// hay que mover lo que la usa (las dos FK son Restrict).
    /// </summary>
    public partial class QuitarEstadoEnProceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El historial En proceso pasa a Pendiente sin perder quién ni cuándo. Dice "Estaba" y no "Pasó a" porque
            // algunas de esas entradas son un "Pedido modificado" o un "Asignado a …" hecho mientras estaba En proceso.
            migrationBuilder.Sql("""
                UPDATE `HistorialEstadosPedido`
                SET `Observacion` = CASE
                        WHEN `Observacion` IS NULL OR TRIM(`Observacion`) = '' THEN 'Estaba En proceso (estado quitado el 30/10/2026).'
                        ELSE CONCAT('Estaba En proceso (estado quitado el 30/10/2026). ', `Observacion`)
                    END,
                    `EstadoPedidoId` = 1
                WHERE `EstadoPedidoId` = 2;
                """);

            // En proceso quería decir "todavía no salió": esos pedidos vuelven a Pendiente.
            migrationBuilder.Sql("UPDATE `Pedidos` SET `EstadoPedidoId` = 1 WHERE `EstadoPedidoId` = 2;");

            migrationBuilder.DeleteData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 3,
                column: "Orden",
                value: 2);

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 4,
                column: "Orden",
                value: 3);

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 5,
                column: "Orden",
                value: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a crear el estado, pero no puede devolver los pedidos a En proceso: quedan Pendiente, y su historial
            // conserva la nota "Estaba En proceso…".
            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 3,
                column: "Orden",
                value: 3);

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 4,
                column: "Orden",
                value: 4);

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 5,
                column: "Orden",
                value: 5);

            migrationBuilder.InsertData(
                table: "EstadosPedido",
                columns: new[] { "Id", "Color", "EsEstadoFinal", "Nombre", "Orden" },
                values: new object[] { 2, "#3b82f6", false, "En proceso", 2 });
        }
    }
}
