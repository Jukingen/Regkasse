using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftDailyClosingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_count",
                table: "cashier_shifts",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "daily_closing_id",
                table: "cashier_shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_daily_closing_id",
                table: "cashier_shifts",
                column: "daily_closing_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_shifts_DailyClosings_daily_closing_id",
                table: "cashier_shifts",
                column: "daily_closing_id",
                principalTable: "DailyClosings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cashier_shifts_DailyClosings_daily_closing_id",
                table: "cashier_shifts");

            migrationBuilder.DropIndex(
                name: "IX_cashier_shifts_daily_closing_id",
                table: "cashier_shifts");

            migrationBuilder.DropColumn(
                name: "cash_count",
                table: "cashier_shifts");

            migrationBuilder.DropColumn(
                name: "daily_closing_id",
                table: "cashier_shifts");
        }
    }
}
