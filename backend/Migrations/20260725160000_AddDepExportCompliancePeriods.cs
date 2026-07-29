using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Period-based DEP export compliance tracking (yearly / quarterly / monthly obligations).
/// Distinct from cron table dep_export_schedules.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725160000_AddDepExportCompliancePeriods")]
public partial class AddDepExportCompliancePeriods : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dep_export_compliance_periods",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                period_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                exported_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                history_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dep_export_compliance_periods", x => x.id);
                table.ForeignKey(
                    name: "FK_dep_export_compliance_periods_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_dep_export_compliance_periods_dep_export_history_history_id",
                    column: x => x.history_id,
                    principalTable: "dep_export_history",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_compliance_periods_tenant_id",
            table: "dep_export_compliance_periods",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_compliance_periods_tenant_period_unique",
            table: "dep_export_compliance_periods",
            columns: new[] { "tenant_id", "period_type", "period_start", "period_end" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_compliance_periods_tenant_id_status",
            table: "dep_export_compliance_periods",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_compliance_periods_history_id",
            table: "dep_export_compliance_periods",
            column: "history_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dep_export_compliance_periods");
    }
}
