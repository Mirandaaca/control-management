using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarSupervisorDeZonaAPromotor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_IdSupervisor",
                table: "Zonas");

            migrationBuilder.RenameColumn(
                name: "IdSupervisor",
                table: "Zonas",
                newName: "PromotorId");

            migrationBuilder.RenameIndex(
                name: "IX_Zonas_IdSupervisor",
                table: "Zonas",
                newName: "IX_Zonas_PromotorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas",
                column: "PromotorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // La columna vieja guardaba supervisores (hoy, gerentes generales), no promotores. Una zona cuyo usuario no
            // recorre rutas queda sin promotor: el gerente asigna el correcto desde Zonas y los clientes pasan a él (7.2).
            // Los clientes conservan su promotor hasta entonces. Si esta migración corre antes de que se siembren los
            // permisos, ninguna zona conserva el suyo, y está bien: todos los valores viejos eran supervisores.
            migrationBuilder.Sql(@"
                UPDATE Zonas z SET z.PromotorId = NULL
                WHERE z.PromotorId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM AspNetUserRoles ur
                    JOIN RolesPermisos rp ON rp.RolId = ur.RoleId
                    JOIN Permisos p ON p.Id = rp.PermisoId
                    WHERE ur.UserId = z.PromotorId AND p.Clave = 'territorial.recorrer-ruta');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas");

            migrationBuilder.RenameColumn(
                name: "PromotorId",
                table: "Zonas",
                newName: "IdSupervisor");

            migrationBuilder.RenameIndex(
                name: "IX_Zonas_PromotorId",
                table: "Zonas",
                newName: "IX_Zonas_IdSupervisor");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_IdSupervisor",
                table: "Zonas",
                column: "IdSupervisor",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
