using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseActivationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_activation_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    machine_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    activation_status = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deactivated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_activation_attempts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_activated_at_utc",
                table: "license_activation_attempts",
                column: "activated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_activation_status",
                table: "license_activation_attempts",
                column: "activation_status");

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_license_key_activated_at_utc",
                table: "license_activation_attempts",
                columns: new[] { "license_key", "activated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_machine_fingerprint",
                table: "license_activation_attempts",
                column: "machine_fingerprint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_activation_attempts");
        }
    }
}
