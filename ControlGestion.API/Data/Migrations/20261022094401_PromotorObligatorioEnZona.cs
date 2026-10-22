using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromotorObligatorioEnZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL no deja pasar a NOT NULL una columna con una FK SET NULL: primero sale la FK.
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas");

            // Editado a mano: EF proponía cambiar los promotores vacíos por "", un usuario que no existe. Si una base
            // tuviera una zona sin promotor, el ALTER falla a la vista: hay que elegirle uno antes de migrar.
            migrationBuilder.AlterColumn<string>(
                name: "PromotorId",
                table: "Zonas",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas",
                column: "PromotorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas");

            migrationBuilder.AlterColumn<string>(
                name: "PromotorId",
                table: "Zonas",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas",
                column: "PromotorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
