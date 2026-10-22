using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuitarEstadoConDeudaVencida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de borrar el estado, sus clientes vuelven al ciclo de visitas (5.2): Activo si su próxima visita es de
            // mañana en adelante, en días de Bolivia (UTC−4, sin horario de verano); si no, o sin visitas, Disponible.
            migrationBuilder.Sql("""
                UPDATE Clientes
                SET EstadoClienteId = CASE
                    WHEN ProximaVisita >= TIMESTAMP(DATE(UTC_TIMESTAMP() - INTERVAL 4 HOUR) + INTERVAL 1 DAY) + INTERVAL 4 HOUR THEN 1
                    ELSE 2
                END
                WHERE EstadoClienteId = 3;
                """);

            migrationBuilder.DeleteData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve el estado, pero no a sus clientes: ya no se sabe cuáles lo tenían.
            migrationBuilder.InsertData(
                table: "EstadosCliente",
                columns: new[] { "Id", "Activo", "Color", "Descripcion", "Nombre" },
                values: new object[] { 3, true, "#ef4444", "Tiene cobranzas vencidas pendientes", "Con Deuda Vencida" });
        }
    }
}
