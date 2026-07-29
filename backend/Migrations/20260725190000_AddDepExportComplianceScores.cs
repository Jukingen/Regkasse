using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Persisted snapshots for DEP export compliance score history.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725190000_AddDepExportComplianceScores")]
public partial class AddDepExportComplianceScores : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dep_export_compliance_scores",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                score = table.Column<int>(type: "integer", nullable: false),
                grade = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                factors_json = table.Column<string>(type: "jsonb", nullable: false),
                critical_issues_json = table.Column<string>(type: "jsonb", nullable: false),
                warnings_json = table.Column<string>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dep_export_compliance_scores", x => x.id);
                table.ForeignKey(
                    name: "FK_dep_export_compliance_scores_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_compliance_scores_tenant_id_calculated_at",
            table: "dep_export_compliance_scores",
            columns: new[] { "tenant_id", "calculated_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dep_export_compliance_scores");
    }
}
