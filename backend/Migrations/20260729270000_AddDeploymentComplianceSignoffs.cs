using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260729270000_AddDeploymentComplianceSignoffs")]
public partial class AddDeploymentComplianceSignoffs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "deployment_compliance_signoffs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                image_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                checklist_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                signed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                signed_by_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                signed_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                signed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deployment_compliance_signoffs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_deployment_compliance_signoffs_image_tag_stage_signed_at_utc",
            table: "deployment_compliance_signoffs",
            columns: new[] { "image_tag", "stage", "signed_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "deployment_compliance_signoffs");
    }
}
