using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddActivatedLicenseLastSeenAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // activated_licenses was introduced in a non-EF migration file without a Designer entry,
            // so it never ran in `dotnet ef database update`. Ensure the table exists before altering it.
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS activated_licenses (
                    id uuid NOT NULL,
                    license_key character varying(64) NOT NULL,
                    customer_name character varying(256) NOT NULL,
                    valid_until_utc timestamp with time zone NOT NULL,
                    machine_fingerprint character varying(128) NOT NULL,
                    activated_at_utc timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_activated_licenses" PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_activated_licenses_machine_fingerprint_license_key"
                    ON activated_licenses (machine_fingerprint, license_key);
                CREATE INDEX IF NOT EXISTS "IX_activated_licenses_valid_until_utc"
                    ON activated_licenses (valid_until_utc);
                CREATE INDEX IF NOT EXISTS "IX_activated_licenses_activated_at_utc"
                    ON activated_licenses (activated_at_utc);
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_at_utc",
                table: "activated_licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE activated_licenses SET last_seen_at_utc = activated_at_utc WHERE last_seen_at_utc IS NULL");

            migrationBuilder.AlterColumn<DateTime>(
                name: "last_seen_at_utc",
                table: "activated_licenses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_last_seen_at_utc",
                table: "activated_licenses",
                column: "last_seen_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_activated_licenses_last_seen_at_utc",
                table: "activated_licenses");

            migrationBuilder.DropColumn(
                name: "last_seen_at_utc",
                table: "activated_licenses");
        }
    }
}
