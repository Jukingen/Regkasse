using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Per-tenant deployment_history for canary progressive rollouts.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729260000_AddDeploymentHistory")]
public partial class AddDeploymentHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "deployment_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                previous_version = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                run_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                smoke_passed = table.Column<bool>(type: "boolean", nullable: true),
                deployed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                soak_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deployment_history", x => x.id);
                table.ForeignKey(
                    name: "FK_deployment_history_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_deployment_history_tenant_id_deployed_at_utc",
            table: "deployment_history",
            columns: new[] { "tenant_id", "deployed_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_deployment_history_stage_status",
            table: "deployment_history",
            columns: new[] { "stage", "status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "deployment_history");
    }
}
