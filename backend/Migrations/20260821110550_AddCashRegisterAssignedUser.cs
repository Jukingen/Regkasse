using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterAssignedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_user_id",
                table: "cash_registers",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_assigned_user_id",
                table: "cash_registers",
                column: "assigned_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_AspNetUsers_assigned_user_id",
                table: "cash_registers",
                column: "assigned_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_AspNetUsers_assigned_user_id",
                table: "cash_registers");

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_assigned_user_id",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "cash_registers");
        }
    }
}
