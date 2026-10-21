using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <summary>
    /// Anexo pasa a Evidencia (7.5), con vínculos explícitos: Evidencia.EventoId y PagoCobranza.EvidenciaId (único). Se
    /// renombra la tabla, no se borra y se crea, para no perder las filas: EF propuso borrar y crear, y se escribió a mano.
    /// </summary>
    public partial class AnexoPasaAEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL no deja tocar un índice que usa una FK (error 1553): las FK salen antes y vuelven al final.
            migrationBuilder.DropForeignKey(name: "FK_PagosCobranza_Anexos_AnexoId", table: "PagosCobranza");
            migrationBuilder.DropForeignKey(name: "FK_Anexos_Eventos_EventoId", table: "Anexos");
            migrationBuilder.DropForeignKey(name: "FK_Anexos_AspNetUsers_SubidoPorId", table: "Anexos");
            migrationBuilder.DropIndex(name: "IX_PagosCobranza_AnexoId", table: "PagosCobranza");

            migrationBuilder.RenameColumn(name: "AnexoId", table: "PagosCobranza", newName: "EvidenciaId");
            migrationBuilder.RenameTable(name: "Anexos", newName: "Evidencias");
            migrationBuilder.RenameIndex(name: "IX_Anexos_EventoId", table: "Evidencias", newName: "IX_Evidencias_EventoId");
            migrationBuilder.RenameIndex(name: "IX_Anexos_SubidoPorId", table: "Evidencias", newName: "IX_Evidencias_SubidoPorId");

            // Las fotos pasan de wwwroot/uploads a la carpeta de archivos (Almacenamiento:Ruta), que guarda aaaa/mm/archivo:
            // "/uploads/2026/10/eventos/x.png" queda "2026/10/x.png". El archivo se muda a mano al aplicar la migración.
            migrationBuilder.Sql(
                "UPDATE Evidencias SET RutaAlmacenamiento = REPLACE(REPLACE(RutaAlmacenamiento, '/uploads/', ''), '/eventos/', '/') " +
                "WHERE RutaAlmacenamiento LIKE '/uploads/%';");

            // Con los vínculos explícitos sobra el vínculo polimórfico, y todas las evidencias son imágenes.
            migrationBuilder.DropColumn(name: "TablaOrigen", table: "Evidencias");
            migrationBuilder.DropColumn(name: "RegistroOrigenId", table: "Evidencias");
            migrationBuilder.DropColumn(name: "EsImagen", table: "Evidencias");

            migrationBuilder.AlterColumn<string>(
                name: "NombreArchivo", table: "Evidencias", type: "varchar(255)", maxLength: 255, nullable: false,
                oldClrType: typeof(string), oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "RutaAlmacenamiento", table: "Evidencias", type: "varchar(300)", maxLength: 300, nullable: false,
                oldClrType: typeof(string), oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "TipoMIME", table: "Evidencias", type: "varchar(50)", maxLength: 50, nullable: false,
                oldClrType: typeof(string), oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "Descripcion", table: "Evidencias", type: "varchar(500)", maxLength: 500, nullable: true,
                oldClrType: typeof(string), oldType: "longtext", oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(name: "IX_PagosCobranza_EvidenciaId", table: "PagosCobranza", column: "EvidenciaId", unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_AspNetUsers_SubidoPorId", table: "Evidencias", column: "SubidoPorId",
                principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Eventos_EventoId", table: "Evidencias", column: "EventoId",
                principalTable: "Eventos", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_PagosCobranza_Evidencias_EvidenciaId", table: "PagosCobranza", column: "EvidenciaId",
                principalTable: "Evidencias", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_PagosCobranza_Evidencias_EvidenciaId", table: "PagosCobranza");
            migrationBuilder.DropForeignKey(name: "FK_Evidencias_Eventos_EventoId", table: "Evidencias");
            migrationBuilder.DropForeignKey(name: "FK_Evidencias_AspNetUsers_SubidoPorId", table: "Evidencias");
            migrationBuilder.DropIndex(name: "IX_PagosCobranza_EvidenciaId", table: "PagosCobranza");

            migrationBuilder.AlterColumn<string>(
                name: "NombreArchivo", table: "Evidencias", type: "longtext", nullable: false,
                oldClrType: typeof(string), oldType: "varchar(255)", oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "RutaAlmacenamiento", table: "Evidencias", type: "longtext", nullable: false,
                oldClrType: typeof(string), oldType: "varchar(300)", oldMaxLength: 300)
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "TipoMIME", table: "Evidencias", type: "longtext", nullable: false,
                oldClrType: typeof(string), oldType: "varchar(50)", oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AlterColumn<string>(
                name: "Descripcion", table: "Evidencias", type: "longtext", nullable: true,
                oldClrType: typeof(string), oldType: "varchar(500)", oldMaxLength: 500, oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4").OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(name: "TablaOrigen", table: "Evidencias", type: "longtext", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "RegistroOrigenId", table: "Evidencias", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "EsImagen", table: "Evidencias", type: "tinyint(1)", nullable: false, defaultValue: true);
            migrationBuilder.Sql(
                "UPDATE Evidencias SET TablaOrigen = 'Evento', RegistroOrigenId = EventoId WHERE EventoId IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE Evidencias e JOIN PagosCobranza p ON p.EvidenciaId = e.Id " +
                "SET e.TablaOrigen = 'PagoCobranza', e.RegistroOrigenId = p.Id;");
            migrationBuilder.Sql(
                "UPDATE Evidencias SET RutaAlmacenamiento = CONCAT('/uploads/', LEFT(RutaAlmacenamiento, 8), 'eventos/', " +
                "SUBSTRING(RutaAlmacenamiento, 9)) WHERE RutaAlmacenamiento NOT LIKE '/uploads/%';");

            migrationBuilder.RenameIndex(name: "IX_Evidencias_SubidoPorId", table: "Evidencias", newName: "IX_Anexos_SubidoPorId");
            migrationBuilder.RenameIndex(name: "IX_Evidencias_EventoId", table: "Evidencias", newName: "IX_Anexos_EventoId");
            migrationBuilder.RenameTable(name: "Evidencias", newName: "Anexos");
            migrationBuilder.RenameColumn(name: "EvidenciaId", table: "PagosCobranza", newName: "AnexoId");

            migrationBuilder.CreateIndex(name: "IX_PagosCobranza_AnexoId", table: "PagosCobranza", column: "AnexoId");
            migrationBuilder.AddForeignKey(
                name: "FK_Anexos_AspNetUsers_SubidoPorId", table: "Anexos", column: "SubidoPorId",
                principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Anexos_Eventos_EventoId", table: "Anexos", column: "EventoId",
                principalTable: "Eventos", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(
                name: "FK_PagosCobranza_Anexos_AnexoId", table: "PagosCobranza", column: "AnexoId",
                principalTable: "Anexos", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        }
    }
}
