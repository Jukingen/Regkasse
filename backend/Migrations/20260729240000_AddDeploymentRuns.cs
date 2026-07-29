using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Platform deployment_runs table for CI status dashboard.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729240000_AddDeploymentRuns")]
public partial class AddDeploymentRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "deployment_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                git_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                image_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                tenant_ids_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                run_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deployment_runs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_deployment_runs_stage_updated_at_utc",
            table: "deployment_runs",
            columns: new[] { "stage", "updated_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_deployment_runs_run_url_stage",
            table: "deployment_runs",
            columns: new[] { "run_url", "stage" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "deployment_runs");
    }
}
