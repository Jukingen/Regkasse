using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723200000_AddApprovalRequests")]
public partial class AddApprovalRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "approval_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                requested_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                approved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "text", nullable: true),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                path_hint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_approval_requests", x => x.id);
                table.ForeignKey(
                    name: "FK_approval_requests_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_approval_requests_status",
            table: "approval_requests",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_approval_requests_requested_at",
            table: "approval_requests",
            column: "requested_at");

        migrationBuilder.CreateIndex(
            name: "idx_approval_requests_tenant_id",
            table: "approval_requests",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_approval_requests_requester_action_tenant",
            table: "approval_requests",
            columns: new[] { "requested_by", "action_type", "tenant_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "approval_requests");
    }
}
