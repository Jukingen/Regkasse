using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723230000_AddGracePeriodPendings")]
public partial class AddGracePeriodPendings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "grace_period_pendings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                action_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                cancelled_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                operation_log_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_grace_period_pendings", x => x.id);
                table.ForeignKey(
                    name: "FK_grace_period_pendings_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_grace_period_pendings_tenant_id",
            table: "grace_period_pendings",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_grace_period_pendings_tenant_status_expires",
            table: "grace_period_pendings",
            columns: new[] { "tenant_id", "status", "expires_at" });

        migrationBuilder.CreateIndex(
            name: "idx_grace_period_pendings_tenant_action_entity_status",
            table: "grace_period_pendings",
            columns: new[] { "tenant_id", "action_kind", "entity_id", "status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "grace_period_pendings");
    }
}
