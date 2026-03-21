using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class CashRegisterCurrentUserFkSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                table: "cash_registers");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                table: "cash_registers",
                column: "CurrentUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                table: "cash_registers");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                table: "cash_registers",
                column: "CurrentUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
