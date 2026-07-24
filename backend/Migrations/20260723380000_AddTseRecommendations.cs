using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723380000_AddTseRecommendations")]
public partial class AddTseRecommendations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_recommendations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                impact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                estimated_savings = table.Column<int>(type: "integer", nullable: false),
                effort_score = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_applied = table.Column<bool>(type: "boolean", nullable: false),
                applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                applied_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                is_dismissed = table.Column<bool>(type: "boolean", nullable: false),
                dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                dismissed_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                rating = table.Column<int>(type: "integer", nullable: false),
                rated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                rated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_recommendations", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_recommendations_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_recommendations_tenant_created",
            table: "tse_recommendations",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_recommendations_tenant_open_code",
            table: "tse_recommendations",
            columns: new[] { "tenant_id", "code", "is_applied", "is_dismissed" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_recommendations");
    }
}
