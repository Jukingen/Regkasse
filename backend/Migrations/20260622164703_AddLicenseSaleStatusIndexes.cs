using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseSaleStatusIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE license_sales
                SET updated_at = created_at
                WHERE updated_at IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "license_sales",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_status",
                table: "license_sales",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_valid_until_utc",
                table: "license_sales",
                column: "valid_until_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_license_sales_status",
                table: "license_sales");

            migrationBuilder.DropIndex(
                name: "idx_license_sales_valid_until_utc",
                table: "license_sales");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "license_sales",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
