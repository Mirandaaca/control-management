using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlGestion.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarSupervisorDeZonaAPromotor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_IdSupervisor",
                table: "Zonas");

            migrationBuilder.RenameColumn(
                name: "IdSupervisor",
                table: "Zonas",
                newName: "PromotorId");

            migrationBuilder.RenameIndex(
                name: "IX_Zonas_IdSupervisor",
                table: "Zonas",
                newName: "IX_Zonas_PromotorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas",
                column: "PromotorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zonas_AspNetUsers_PromotorId",
                table: "Zonas");

            migrationBuilder.RenameColumn(
                name: "PromotorId",
                table: "Zonas",
                newName: "IdSupervisor");

            migrationBuilder.RenameIndex(
                name: "IX_Zonas_PromotorId",
                table: "Zonas",
                newName: "IX_Zonas_IdSupervisor");

            migrationBuilder.AddForeignKey(
                name: "FK_Zonas_AspNetUsers_IdSupervisor",
                table: "Zonas",
                column: "IdSupervisor",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
