using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase2_ClientesInventarioHP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstadosCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosCliente", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InventariosProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    StockActual = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    StockMinimo = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    StockMaximo = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    VersionConcurrencia = table.Column<uint>(type: "int unsigned", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventariosProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventariosProducto_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    StockAnterior = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    StockResultante = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    ReferenciaTabla = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenciaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Fecha = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Observacion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Codigo = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NombreCompleto = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RazonSocial = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NIT = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefono = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DireccionTexto = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitud = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Longitud = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    EstadoClienteId = table.Column<int>(type: "int", nullable: false),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiasRevisita = table.Column<int>(type: "int", nullable: false),
                    UltimaVisita = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProximaVisita = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TieneCredito = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LimiteCredito = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoPendiente = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreadoPorId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Clientes_EstadosCliente_EstadoClienteId",
                        column: x => x.EstadoClienteId,
                        principalTable: "EstadosCliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clientes_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HojasPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Numero = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Latitud = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Longitud = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false),
                    PrecisionGPS = table.Column<float>(type: "float", nullable: true),
                    TipoVenta = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstadoHP = table.Column<int>(type: "int", nullable: false),
                    ObservacionesAnulacion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AnuladoPorId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HojasPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HojasPedido_AspNetUsers_AnuladoPorId",
                        column: x => x.AnuladoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HojasPedido_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasPedido_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasPedido_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DetallesHP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HojaPedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesHP", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesHP_HojasPedido_HojaPedidoId",
                        column: x => x.HojaPedidoId,
                        principalTable: "HojasPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesHP_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "EstadosCliente",
                columns: new[] { "Id", "Activo", "Color", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "#10b981", "Cliente activo y disponible", "Activo" },
                    { 2, true, "#f59e0b", "Ha pasado el período de re-visita", "Disponible para Re-visita" },
                    { 3, true, "#ef4444", "Tiene cobranzas vencidas pendientes", "Con Deuda Vencida" },
                    { 4, true, "#6b7280", "Cliente inactivo o dado de baja", "Inactivo" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Codigo",
                table: "Clientes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CreadoPorId",
                table: "Clientes",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EstadoClienteId",
                table: "Clientes",
                column: "EstadoClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_PromotorId",
                table: "Clientes",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ZonaId",
                table: "Clientes",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesHP_HojaPedidoId",
                table: "DetallesHP",
                column: "HojaPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesHP_ProductoId",
                table: "DetallesHP",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_AnuladoPorId",
                table: "HojasPedido",
                column: "AnuladoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_ClienteId",
                table: "HojasPedido",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_Numero",
                table: "HojasPedido",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_PromotorId",
                table: "HojasPedido",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasPedido_ZonaId",
                table: "HojasPedido",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_InventariosProducto_ProductoId",
                table: "InventariosProducto",
                column: "ProductoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_ProductoId",
                table: "MovimientosInventario",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_UsuarioId",
                table: "MovimientosInventario",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesHP");

            migrationBuilder.DropTable(
                name: "InventariosProducto");

            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "HojasPedido");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "EstadosCliente");
        }
    }
}
