using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723310000_AddTseDrRunbooks")]
public partial class AddTseDrRunbooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_dr_runbooks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                scenario = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                estimated_rto_minutes = table.Column<int>(type: "integer", nullable: false),
                actual_rto_minutes = table.Column<int>(type: "integer", nullable: false),
                is_drill = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_tested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_dr_runbooks", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_dr_runbooks_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tse_dr_steps",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                runbook_id = table.Column<Guid>(type: "uuid", nullable: false),
                step_order = table.Column<int>(type: "integer", nullable: false),
                action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                is_automated = table.Column<bool>(type: "boolean", nullable: false),
                is_completed = table.Column<bool>(type: "boolean", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_dr_steps", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_dr_steps_runbooks",
                    column: x => x.runbook_id,
                    principalTable: "tse_dr_runbooks",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_dr_runbooks_tenant_created",
            table: "tse_dr_runbooks",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_dr_runbooks_status",
            table: "tse_dr_runbooks",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_tse_dr_steps_runbook_id",
            table: "tse_dr_steps",
            column: "runbook_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_dr_steps");
        migrationBuilder.DropTable(name: "tse_dr_runbooks");
    }
}
