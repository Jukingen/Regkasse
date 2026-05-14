using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedLicenseLifecycleFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at_utc",
                table: "issued_licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by_user_id",
                table: "issued_licenses",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "issued_licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_user_id",
                table: "issued_licenses",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cancelled",
                table: "issued_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "issued_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_cancelled",
                table: "issued_licenses",
                column: "is_cancelled",
                filter: "is_cancelled = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_deleted",
                table: "issued_licenses",
                column: "is_deleted",
                filter: "is_deleted = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_issued_licenses_is_cancelled",
                table: "issued_licenses");

            migrationBuilder.DropIndex(
                name: "IX_issued_licenses_is_deleted",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "cancelled_at_utc",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "is_cancelled",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "issued_licenses");
        }
    }
}
