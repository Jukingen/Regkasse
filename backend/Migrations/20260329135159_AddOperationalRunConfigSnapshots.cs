using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalRunConfigSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "config_snapshot_json",
                table: "restore_verification_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "config_snapshot_json",
                table: "backup_runs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "config_snapshot_json",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "config_snapshot_json",
                table: "backup_runs");
        }
    }
}
