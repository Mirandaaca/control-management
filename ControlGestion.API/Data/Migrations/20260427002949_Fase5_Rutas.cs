using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase5_Rutas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rutas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Numero = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    FechaPlanificada = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rutas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rutas_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rutas_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RutasDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RutaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstadoVisita = table.Column<int>(type: "int", nullable: false),
                    HoraVisita = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Latitud = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    Longitud = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    Observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RutasDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RutasDetalle_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RutasDetalle_Rutas_RutaId",
                        column: x => x.RutaId,
                        principalTable: "Rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_Numero",
                table: "Rutas",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_PromotorId",
                table: "Rutas",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_ZonaId",
                table: "Rutas",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_RutasDetalle_ClienteId",
                table: "RutasDetalle",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_RutasDetalle_RutaId",
                table: "RutasDetalle",
                column: "RutaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RutasDetalle");

            migrationBuilder.DropTable(
                name: "Rutas");
        }
    }
}
