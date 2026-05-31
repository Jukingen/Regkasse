using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLicenseGraceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "license_grace_period_started_at",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "license_grace_period_used_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_license_valid_until",
                table: "tenants",
                column: "license_valid_until_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_license_valid_until",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "license_grace_period_started_at",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "license_grace_period_used_days",
                table: "tenants");
        }
    }
}
