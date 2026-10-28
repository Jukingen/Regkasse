using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoredDatabaseApplicationSmokeRunColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "restored_database_application_smoke_executed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "restored_database_application_smoke_passed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restored_database_application_smoke_result_kind",
                table: "restore_verification_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "restored_database_application_smoke_executed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restored_database_application_smoke_passed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restored_database_application_smoke_result_kind",
                table: "restore_verification_runs");
        }
    }
}
