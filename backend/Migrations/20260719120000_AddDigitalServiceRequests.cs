using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260719120000_AddDigitalServiceRequests")]
public partial class AddDigitalServiceRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "digital_service_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                resolved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_digital_service_requests", x => x.id);
                table.ForeignKey(
                    name: "FK_digital_service_requests_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_requests_tenant_id",
            table: "digital_service_requests",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_digital_service_requests_pending_tenant_type",
            table: "digital_service_requests",
            columns: new[] { "tenant_id", "service_type" },
            unique: true,
            filter: "status = 'Pending'");

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_requests_status_requested",
            table: "digital_service_requests",
            columns: new[] { "status", "requested_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "digital_service_requests");
    }
}
