using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSecuenciasDeNumeracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Secuencias",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ultimo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secuencias", x => x.Clave);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Cada secuencia arranca en el número más alto que ya existe ("HP-2026-00019" -> clave HP-2026, último 19),
            // para no volver a entregar un número que ya está en uso.
            foreach (var tabla in new[] { "HojasPedido", "Cobranzas", "Pedidos", "Eventos", "Rutas" })
                migrationBuilder.Sql($"""
                    INSERT INTO Secuencias (Clave, Ultimo)
                    SELECT SUBSTRING_INDEX(Numero, '-', 2), MAX(CAST(SUBSTRING_INDEX(Numero, '-', -1) AS UNSIGNED))
                    FROM {tabla}
                    GROUP BY SUBSTRING_INDEX(Numero, '-', 2);
                    """);

            migrationBuilder.Sql("""
                INSERT INTO Secuencias (Clave, Ultimo)
                SELECT 'CLI', MAX(CAST(SUBSTRING_INDEX(Codigo, '-', -1) AS UNSIGNED))
                FROM Clientes
                HAVING COUNT(*) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Secuencias");
        }
    }
}
