using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260721140000_AddAdminUserFeedback")]
public partial class AddAdminUserFeedback : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_user_feedback",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                rating = table.Column<int>(type: "integer", nullable: true),
                page_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                submitted_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                submitted_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reviewed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reviewer_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_user_feedback", x => x.id);
                table.ForeignKey(
                    name: "FK_admin_user_feedback_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_admin_user_feedback_tenant_id",
            table: "admin_user_feedback",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_admin_user_feedback_submitted_by",
            table: "admin_user_feedback",
            column: "submitted_by_user_id");

        migrationBuilder.CreateIndex(
            name: "idx_admin_user_feedback_status_created",
            table: "admin_user_feedback",
            columns: new[] { "status", "created_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admin_user_feedback");
    }
}
