using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedLicenseSupersededByLicenseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "superseded_by_license_id",
                table: "issued_licenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_superseded_by_license_id",
                table: "issued_licenses",
                column: "superseded_by_license_id");

            migrationBuilder.AddForeignKey(
                name: "FK_issued_licenses_issued_licenses_superseded_by_license_id",
                table: "issued_licenses",
                column: "superseded_by_license_id",
                principalTable: "issued_licenses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issued_licenses_issued_licenses_superseded_by_license_id",
                table: "issued_licenses");

            migrationBuilder.DropIndex(
                name: "IX_issued_licenses_superseded_by_license_id",
                table: "issued_licenses");

            migrationBuilder.DropColumn(
                name: "superseded_by_license_id",
                table: "issued_licenses");
        }
    }
}
