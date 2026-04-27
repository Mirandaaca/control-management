using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fix_VersionConcurrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "VersionConcurrencia",
                table: "InventariosProducto",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<uint>(
                name: "VersionConcurrencia",
                table: "InventariosProducto",
                type: "int unsigned",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(6)",
                oldRowVersion: true);
        }
    }
}
