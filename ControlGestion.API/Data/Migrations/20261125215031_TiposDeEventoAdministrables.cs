using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <summary>
    /// Los tipos de evento se administran desde el panel (tarea 32):
    /// - el nombre pasa de longtext a varchar(80) con un índice único. La intercalación del juego utf8mb4 no distingue
    ///   mayúsculas ni tildes, como en Categorias. Hasta esta migración solo existían los 5 sembrados (no había cómo
    ///   agregar otros), así que no hay nombres repetidos ni de más de 80;
    /// - sale RequiereGeolocalizacion, que nadie leía: todo evento guarda su ubicación (7.3.13, ext. 2).
    /// El Down la devuelve en true para todas las filas, como estaba (era el valor por defecto de cada tipo).
    /// </summary>
    public partial class TiposDeEventoAdministrables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiereGeolocalizacion",
                table: "TiposEvento");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TiposEvento",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TiposEvento_Nombre",
                table: "TiposEvento",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TiposEvento_Nombre",
                table: "TiposEvento");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TiposEvento",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "RequiereGeolocalizacion",
                table: "TiposEvento",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }
    }
}
