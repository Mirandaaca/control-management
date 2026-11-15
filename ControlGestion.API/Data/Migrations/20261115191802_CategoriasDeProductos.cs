using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <summary>
    /// La categoría del producto pasa de un texto libre a la tabla Categorias (tarea 29). Escrita a mano: EF borraba el
    /// texto antes de leerlo y agregaba CategoriaId con 0, que la llave foránea rechaza.
    /// - Una categoría por cada texto distinto, recortado y sin los vacíos. La intercalación de la base no distingue
    ///   mayúsculas ni tildes, así que "Limpieza", " limpieza " y "LÍMPIEZA" son la misma; el nombre queda como lo escribió
    ///   el producto más antiguo (el Id menor).
    /// - Los productos sin categoría quedan en "Sin categoría", que se crea solo si hace falta.
    /// - El SQL usa los nombres de las tablas tal como los crea EF: así funciona con y sin lower_case_table_names=1.
    /// </summary>
    public partial class CategoriasDeProductos : Migration
    {
        private const string SinCategoria = "Sin categoría";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias",
                column: "Nombre",
                unique: true);

            // Una por grupo: la fila del producto más antiguo de cada texto (TRIM de NULL no pasa el filtro).
            migrationBuilder.Sql("""
                INSERT INTO Categorias (Nombre)
                SELECT TRIM(p.Categoria)
                FROM Productos p
                WHERE TRIM(p.Categoria) <> ''
                  AND p.Id = (SELECT MIN(o.Id) FROM Productos o WHERE TRIM(o.Categoria) = TRIM(p.Categoria));
                """);
            migrationBuilder.Sql($"""
                INSERT INTO Categorias (Nombre)
                SELECT '{SinCategoria}' FROM DUAL
                WHERE EXISTS (SELECT 1 FROM Productos WHERE Categoria IS NULL OR TRIM(Categoria) = '')
                  AND NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = '{SinCategoria}');
                """);

            // Primero nula, para llenarla; después obligatoria, con su índice y su llave foránea.
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Productos p JOIN Categorias c ON c.Nombre = TRIM(p.Categoria)
                SET p.CategoriaId = c.Id
                WHERE TRIM(p.Categoria) <> '';
                """);
            migrationBuilder.Sql($"""
                UPDATE Productos p JOIN Categorias c ON c.Nombre = '{SinCategoria}'
                SET p.CategoriaId = c.Id
                WHERE p.CategoriaId IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CategoriaId",
                table: "Productos",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos",
                column: "CategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Productos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve el texto con el nombre de su categoría; los de "Sin categoría", sin texto, como estaban.
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Productos",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql($"""
                UPDATE Productos p JOIN Categorias c ON c.Id = p.CategoriaId
                SET p.Categoria = c.Nombre
                WHERE c.Nombre <> '{SinCategoria}';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "Productos");

            migrationBuilder.DropTable(
                name: "Categorias");
        }
    }
}
