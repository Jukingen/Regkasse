using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalDependencyL6RunColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_dependency_l6_overall_state",
                table: "restore_verification_runs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_dependency_l6_summary",
                table: "restore_verification_runs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "external_dependency_l6_overall_state",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "external_dependency_l6_summary",
                table: "restore_verification_runs");
        }
    }
}
