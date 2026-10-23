using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class NombresConMayusculaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "En proceso");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nombre",
                value: "En ruta");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 1,
                column: "Nombre",
                value: "Cliente ausente");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Entrega no realizada");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nombre",
                value: "Problema de acceso");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 4,
                column: "Nombre",
                value: "Incidencia de cobro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "En Proceso");

            migrationBuilder.UpdateData(
                table: "EstadosPedido",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nombre",
                value: "En Ruta");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 1,
                column: "Nombre",
                value: "Cliente Ausente");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Entrega No Realizada");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nombre",
                value: "Problema de Acceso");

            migrationBuilder.UpdateData(
                table: "TiposEvento",
                keyColumn: "Id",
                keyValue: 4,
                column: "Nombre",
                value: "Incidencia de Cobro");
        }
    }
}
