using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260719240000_AddTenantDataRightsRequests")]
public partial class AddTenantDataRightsRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_data_rights_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                approval_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                processing_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ready_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                artifact_relative_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                artifact_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                artifact_byte_size = table.Column<long>(type: "bigint", nullable: true),
                view_payload_json = table.Column<string>(type: "text", nullable: true),
                linked_deletion_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_data_rights_requests", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_data_rights_requests_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tenant_data_rights_requests_deletion_request_id",
                    column: x => x.linked_deletion_request_id,
                    principalTable: "tenant_data_deletion_requests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_data_rights_requests_tenant_id",
            table: "tenant_data_rights_requests",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_tenant_data_rights_requests_tenant_type_status",
            table: "tenant_data_rights_requests",
            columns: new[] { "tenant_id", "request_type", "status" });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_data_rights_requests_linked_deletion",
            table: "tenant_data_rights_requests",
            column: "linked_deletion_request_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tenant_data_rights_requests");
    }
}
