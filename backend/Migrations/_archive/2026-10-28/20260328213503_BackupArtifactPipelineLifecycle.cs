using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class BackupArtifactPipelineLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_redacted_locator",
                table: "backup_artifacts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lifecycle_state",
                table: "backup_artifacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Pre-pipeline rows already passed staging verification; external copy was not tracked.
            migrationBuilder.Sql("UPDATE backup_artifacts SET lifecycle_state = 1 WHERE lifecycle_state = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "external_redacted_locator",
                table: "backup_artifacts");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                table: "backup_artifacts");
        }
    }
}
