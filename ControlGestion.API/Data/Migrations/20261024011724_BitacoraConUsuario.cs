using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class BitacoraConUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UsuarioId",
                table: "AuditoriaLogs",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Antes de la FK: los inicios de sesión se guardaban como "Anonimo", que no es un usuario. Quedan sin usuario.
            migrationBuilder.Sql(
                "UPDATE AuditoriaLogs SET UsuarioId = NULL WHERE UsuarioId NOT IN (SELECT Id FROM AspNetUsers);");

            // El módulo era el pedazo de la URL ("cobranzas"); ahora es uno de los 8 del catálogo. La acción vieja queda.
            migrationBuilder.Sql("""
                UPDATE AuditoriaLogs SET Modulo = CASE
                    WHEN Modulo = 'zonas' AND Accion LIKE '%/limite%' THEN 'Visualización Territorial y Re-visitas'
                    WHEN Modulo IN ('auth', 'perfil', 'usuarios', 'roles') THEN 'Usuario, Seguridad y Auditoría'
                    WHEN Modulo = 'pedidos' THEN 'Gestión de Pedidos'
                    WHEN Modulo IN ('hojas-pedido', 'clientes', 'inventario') THEN 'Gestión de Ventas Geolocalizadas'
                    WHEN Modulo = 'cobranzas' THEN 'Gestión de Cobranzas'
                    WHEN Modulo IN ('eventos', 'evidencias') THEN 'Gestión de Eventos y Evidencias'
                    WHEN Modulo IN ('rutas', 'puntos-referencia') THEN 'Visualización Territorial y Re-visitas'
                    WHEN Modulo IN ('zonas', 'productos', 'estados-cliente', 'configuraciones') THEN 'Parámetros y Configuraciones'
                    ELSE Modulo
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaLogs_Fecha",
                table: "AuditoriaLogs",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaLogs_UsuarioId",
                table: "AuditoriaLogs",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditoriaLogs_AspNetUsers_UsuarioId",
                table: "AuditoriaLogs",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditoriaLogs_AspNetUsers_UsuarioId",
                table: "AuditoriaLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditoriaLogs_Fecha",
                table: "AuditoriaLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditoriaLogs_UsuarioId",
                table: "AuditoriaLogs");

            migrationBuilder.UpdateData(
                table: "AuditoriaLogs",
                keyColumn: "UsuarioId",
                keyValue: null,
                column: "UsuarioId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "UsuarioId",
                table: "AuditoriaLogs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
