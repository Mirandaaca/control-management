using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase3_PedidosEventos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstadosPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EsEstadoFinal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PermiteTransicion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosPedido", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TiposEvento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiereEvidencia = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiereGeolocalizacion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NivelPrioridad = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposEvento", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Numero = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RepartidorId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    HojaPedidoId = table.Column<int>(type: "int", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaEntregaEstimada = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaEntregaReal = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LatitudEntrega = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    LongitudEntrega = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    EstadoPedidoId = table.Column<int>(type: "int", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_AspNetUsers_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_EstadosPedido_EstadoPedidoId",
                        column: x => x.EstadoPedidoId,
                        principalTable: "EstadosPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_HojasPedido_HojaPedidoId",
                        column: x => x.HojaPedidoId,
                        principalTable: "HojasPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pedidos_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DetallesPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CantidadEntregada = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Eventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Numero = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TipoEventoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    PedidoId = table.Column<int>(type: "int", nullable: true),
                    PromotorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaEvento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Latitud = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Longitud = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false),
                    Descripcion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstadoEvento = table.Column<int>(type: "int", nullable: false),
                    ResueltoPorId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaResolucion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Eventos_AspNetUsers_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Eventos_AspNetUsers_ResueltoPorId",
                        column: x => x.ResueltoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Eventos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Eventos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Eventos_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HistorialEstadosPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    EstadoPedidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaCambio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Observacion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LatitudCambio = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    LongitudCambio = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPedido_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPedido_EstadosPedido_EstadoPedidoId",
                        column: x => x.EstadoPedidoId,
                        principalTable: "EstadosPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Anexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TablaOrigen = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegistroOrigenId = table.Column<int>(type: "int", nullable: false),
                    NombreArchivo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RutaAlmacenamiento = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TipoMIME = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Latitud = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    Longitud = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    FechaCaptura = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaSubida = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SubidoPorId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EsImagen = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Descripcion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anexos_AspNetUsers_SubidoPorId",
                        column: x => x.SubidoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Anexos_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "EstadosPedido",
                columns: new[] { "Id", "Color", "EsEstadoFinal", "Nombre", "Orden", "PermiteTransicion" },
                values: new object[,]
                {
                    { 1, "#f59e0b", false, "Pendiente", 1, "[2,5]" },
                    { 2, "#3b82f6", false, "En Proceso", 2, "[3,5]" },
                    { 3, "#8b5cf6", false, "En Ruta", 3, "[4,5]" },
                    { 4, "#10b981", true, "Entregado", 4, "[]" },
                    { 5, "#ef4444", true, "Anulado", 5, "[]" }
                });

            migrationBuilder.InsertData(
                table: "TiposEvento",
                columns: new[] { "Id", "Activo", "NivelPrioridad", "Nombre", "RequiereEvidencia", "RequiereGeolocalizacion" },
                values: new object[,]
                {
                    { 1, true, 1, "Cliente Ausente", true, true },
                    { 2, true, 2, "Entrega No Realizada", true, true },
                    { 3, true, 1, "Problema de Acceso", false, true },
                    { 4, true, 2, "Incidencia de Cobro", false, true },
                    { 5, true, 0, "Otro", false, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anexos_EventoId",
                table: "Anexos",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_Anexos_SubidoPorId",
                table: "Anexos",
                column: "SubidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_PedidoId",
                table: "DetallesPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_ProductoId",
                table: "DetallesPedido",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_ClienteId",
                table: "Eventos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_Numero",
                table: "Eventos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_PedidoId",
                table: "Eventos",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_PromotorId",
                table: "Eventos",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_ResueltoPorId",
                table: "Eventos",
                column: "ResueltoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_TipoEventoId",
                table: "Eventos",
                column: "TipoEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedido_EstadoPedidoId",
                table: "HistorialEstadosPedido",
                column: "EstadoPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedido_PedidoId",
                table: "HistorialEstadosPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedido_UsuarioId",
                table: "HistorialEstadosPedido",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_EstadoPedidoId",
                table: "Pedidos",
                column: "EstadoPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_HojaPedidoId",
                table: "Pedidos",
                column: "HojaPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Numero",
                table: "Pedidos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_PromotorId",
                table: "Pedidos",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_RepartidorId",
                table: "Pedidos",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ZonaId",
                table: "Pedidos",
                column: "ZonaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anexos");

            migrationBuilder.DropTable(
                name: "DetallesPedido");

            migrationBuilder.DropTable(
                name: "HistorialEstadosPedido");

            migrationBuilder.DropTable(
                name: "Eventos");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "TiposEvento");

            migrationBuilder.DropTable(
                name: "EstadosPedido");
        }
    }
}
