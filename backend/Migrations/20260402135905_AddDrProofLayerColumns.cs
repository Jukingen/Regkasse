using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDrProofLayerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "application_smoke_probe_executed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "application_smoke_probe_passed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_dependency_proof_outcome",
                table: "restore_verification_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fiscal_continuity_layer_passed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "application_smoke_probe_executed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "application_smoke_probe_passed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "external_dependency_proof_outcome",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "fiscal_continuity_layer_passed",
                table: "restore_verification_runs");
        }
    }
}
