using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedLicensesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issued_licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expiry_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    require_fingerprint = table.Column<bool>(type: "boolean", nullable: false),
                    machine_hash_hex = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    signed_jwt = table.Column<string>(type: "text", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_licenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_expiry_at_utc",
                table: "issued_licenses",
                column: "expiry_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_revoked",
                table: "issued_licenses",
                column: "is_revoked",
                filter: "is_revoked = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_issued_at_utc",
                table: "issued_licenses",
                column: "issued_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_license_key",
                table: "issued_licenses",
                column: "license_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issued_licenses");
        }
    }
}
