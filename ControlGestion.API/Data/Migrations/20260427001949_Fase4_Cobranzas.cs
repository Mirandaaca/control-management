using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase4_Cobranzas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cobranzas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Numero = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    HojaPedidoId = table.Column<int>(type: "int", nullable: true),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoPagado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaPagoCompleto = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobranzas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobranzas_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranzas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobranzas_HojasPedido_HojaPedidoId",
                        column: x => x.HojaPedidoId,
                        principalTable: "HojasPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PagosCobranza",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CobranzaId = table.Column<int>(type: "int", nullable: false),
                    RegistradoPorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MetodoPago = table.Column<int>(type: "int", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Referencia = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnexoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosCobranza", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosCobranza_Anexos_AnexoId",
                        column: x => x.AnexoId,
                        principalTable: "Anexos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PagosCobranza_AspNetUsers_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosCobranza_Cobranzas_CobranzaId",
                        column: x => x.CobranzaId,
                        principalTable: "Cobranzas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_ClienteId",
                table: "Cobranzas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_HojaPedidoId",
                table: "Cobranzas",
                column: "HojaPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_Numero",
                table: "Cobranzas",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cobranzas_PromotorId",
                table: "Cobranzas",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosCobranza_AnexoId",
                table: "PagosCobranza",
                column: "AnexoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosCobranza_CobranzaId",
                table: "PagosCobranza",
                column: "CobranzaId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosCobranza_RegistradoPorId",
                table: "PagosCobranza",
                column: "RegistradoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagosCobranza");

            migrationBuilder.DropTable(
                name: "Cobranzas");
        }
    }
}
