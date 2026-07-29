using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Smoke result + previous image columns on deployment_runs.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729250000_AddDeploymentRunSmokeFields")]
public partial class AddDeploymentRunSmokeFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "smoke_passed",
            table: "deployment_runs",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "smoke_summary",
            table: "deployment_runs",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "previous_image_tag",
            table: "deployment_runs",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "smoke_passed", table: "deployment_runs");
        migrationBuilder.DropColumn(name: "smoke_summary", table: "deployment_runs");
        migrationBuilder.DropColumn(name: "previous_image_tag", table: "deployment_runs");
    }
}
