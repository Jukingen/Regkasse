using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723350000_AddTseWebhooks")]
public partial class AddTseWebhooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_webhooks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                events = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                last_delivery_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_delivery_success = table.Column<bool>(type: "boolean", nullable: true),
                consecutive_failures = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_webhooks", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_webhooks_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tse_webhook_deliveries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                webhook_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                success = table.Column<bool>(type: "boolean", nullable: false),
                http_status = table.Column<int>(type: "integer", nullable: true),
                response_snippet = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                payload_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_webhook_deliveries", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_webhook_deliveries_webhook",
                    column: x => x.webhook_id,
                    principalTable: "tse_webhooks",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_webhooks_tenant",
            table: "tse_webhooks",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_webhook_deliveries_webhook_delivered",
            table: "tse_webhook_deliveries",
            columns: new[] { "webhook_id", "delivered_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_webhook_deliveries");
        migrationBuilder.DropTable(name: "tse_webhooks");
    }
}
