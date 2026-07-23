using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723200000_AddRiskScores")]
public partial class AddRiskScores : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "risk_scores",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                action_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                score = table.Column<int>(type: "integer", nullable: false),
                risk_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                is_resolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                resolution = table.Column<string>(type: "text", nullable: true),
                details_json = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_risk_scores", x => x.id);
                table.ForeignKey(
                    name: "FK_risk_scores_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_risk_scores_tenant_id",
            table: "risk_scores",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_risk_scores_user_id",
            table: "risk_scores",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "idx_risk_scores_created_at",
            table: "risk_scores",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_risk_scores_tenant_resolved_score",
            table: "risk_scores",
            columns: new[] { "tenant_id", "is_resolved", "score" });

        migrationBuilder.Sql(
            """
            ALTER TABLE risk_scores
              ADD CONSTRAINT ck_risk_scores_score
              CHECK (score >= 0 AND score <= 100);

            ALTER TABLE risk_scores
              ADD CONSTRAINT ck_risk_scores_risk_level
              CHECK (risk_level IN ('Low', 'Medium', 'High', 'Critical'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "risk_scores");
    }
}
