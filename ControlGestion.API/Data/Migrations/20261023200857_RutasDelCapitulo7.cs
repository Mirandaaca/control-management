using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <summary>
    /// Las rutas de la 7.3.20 (tarea 14): sin los estados Reprogramado (visita) y Cancelada (ruta), y con la fecha como un
    /// día, único por promotor (ext. 2.3b).
    /// </summary>
    public partial class RutasDelCapitulo7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Una visita reprogramada nunca se marcó: vuelve a pendiente. Una ruta cancelada ya no se recorre: queda cerrada.
            migrationBuilder.Sql(
                "UPDATE RutasDetalle SET EstadoVisita = 0, HoraVisita = NULL, Latitud = NULL, Longitud = NULL WHERE EstadoVisita = 3;");
            migrationBuilder.Sql("UPDATE Rutas SET Estado = 2 WHERE Estado = 3;");

            // Las fechas guardadas eran el día a las 00:00: pasar a date se queda con ese día.
            migrationBuilder.AlterColumn<DateOnly>(
                name: "FechaPlanificada",
                table: "Rutas",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            // Primero el índice nuevo, que empieza por PromotorId: MySQL no deja quitar el viejo mientras la llave foránea
            // del promotor no tenga otro.
            migrationBuilder.CreateIndex(
                name: "IX_Rutas_PromotorId_FechaPlanificada",
                table: "Rutas",
                columns: new[] { "PromotorId", "FechaPlanificada" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Rutas_PromotorId",
                table: "Rutas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Rutas_PromotorId",
                table: "Rutas",
                column: "PromotorId");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_PromotorId_FechaPlanificada",
                table: "Rutas");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaPlanificada",
                table: "Rutas",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }
    }
}
