using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstadosDelClienteConPaleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escrito a mano: EF proponía renombrar Activo a EsDelSistema, lo que habría marcado del sistema a todos los
            // estados (Activo valía 1 en todos). Son dos columnas distintas: una se agrega y la otra se quita.
            migrationBuilder.AddColumn<bool>(
                name: "EsDelSistema",
                table: "EstadosCliente",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "EstadosCliente");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "EstadosCliente",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "EstadosCliente",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "EstadosCliente",
                type: "varchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Color", "EsDelSistema" },
                values: new object[] { "#DC2626", true });

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Color", "EsDelSistema" },
                values: new object[] { "#15803D", true });

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Color", "EsDelSistema" },
                values: new object[] { "#475569", false });

            // Cualquier otro estado manual con un color fuera de la paleta (PaletaDeEstados.Manuales) pasa al color más
            // parecido de ella: el de menor distancia en RGB.
            migrationBuilder.Sql("""
                UPDATE EstadosCliente e
                SET e.Color = (
                    SELECT p.Color FROM (
                        SELECT '#475569' AS Color UNION ALL SELECT '#1E293B' UNION ALL SELECT '#1D4ED8'
                        UNION ALL SELECT '#0369A1' UNION ALL SELECT '#4338CA' UNION ALL SELECT '#6D28D9') p
                    ORDER BY POW(CONV(SUBSTRING(e.Color, 2, 2), 16, 10) - CONV(SUBSTRING(p.Color, 2, 2), 16, 10), 2)
                           + POW(CONV(SUBSTRING(e.Color, 4, 2), 16, 10) - CONV(SUBSTRING(p.Color, 4, 2), 16, 10), 2)
                           + POW(CONV(SUBSTRING(e.Color, 6, 2), 16, 10) - CONV(SUBSTRING(p.Color, 6, 2), 16, 10), 2)
                    LIMIT 1)
                WHERE e.EsDelSistema = 0
                  AND e.Color NOT IN ('#475569', '#1E293B', '#1D4ED8', '#0369A1', '#4338CA', '#6D28D9');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EstadosCliente_Nombre",
                table: "EstadosCliente",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstadosCliente_Nombre",
                table: "EstadosCliente");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "EstadosCliente",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropColumn(
                name: "EsDelSistema",
                table: "EstadosCliente");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "EstadosCliente",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "EstadosCliente",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "EstadosCliente",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(7)",
                oldMaxLength: 7)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 1,
                column: "Color",
                value: "#10b981");

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 2,
                column: "Color",
                value: "#f59e0b");

            migrationBuilder.UpdateData(
                table: "EstadosCliente",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Activo", "Color" },
                values: new object[] { true, "#6b7280" });
        }
    }
}
