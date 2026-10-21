using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstadosYSolucionDelEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Eventos",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Solucion",
                table: "Eventos",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VersionConcurrencia",
                table: "Eventos",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            // EstadoEvento queda en Pendiente (0) y Resuelto (1), como fija el 7.5. Antes era Registrado (0), Revisado (1),
            // Resuelto (2) y Escalado (3): los que no estaban resueltos siguen pendientes. Un resuelto siempre tiene solución.
            migrationBuilder.Sql("UPDATE Eventos SET EstadoEvento = CASE WHEN EstadoEvento = 2 THEN 1 ELSE 0 END;");
            migrationBuilder.Sql(
                "UPDATE Eventos SET Solucion = 'Resuelto antes de la tarea 09, sin solución registrada' " +
                "WHERE EstadoEvento = 1 AND Solucion IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Eventos SET EstadoEvento = CASE WHEN EstadoEvento = 1 THEN 2 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Solucion",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "VersionConcurrencia",
                table: "Eventos");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Eventos",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
