using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public class AddActivatedLicensesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activated_licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    machine_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activated_licenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_machine_fingerprint_license_key",
                table: "activated_licenses",
                columns: new[] { "machine_fingerprint", "license_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_valid_until_utc",
                table: "activated_licenses",
                column: "valid_until_utc");

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_activated_at_utc",
                table: "activated_licenses",
                column: "activated_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activated_licenses");
        }
    }
}
