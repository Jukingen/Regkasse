using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterMonatsbelegJahresbelegUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_jahresbeleg_utc",
                table: "cash_registers",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_monatsbeleg_utc",
                table: "cash_registers",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_jahresbeleg_utc",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "last_monatsbeleg_utc",
                table: "cash_registers");
        }
    }
}
