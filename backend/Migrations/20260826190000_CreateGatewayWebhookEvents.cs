using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Idempotent webhook inbox for payment gateways. Does not create fiscal <c>payment_details</c>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260826190000_CreateGatewayWebhookEvents")]
public partial class CreateGatewayWebhookEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS gateway_webhook_events (
                id uuid NOT NULL,
                tenant_id uuid NULL,
                intent_id uuid NULL,
                provider character varying(32) NOT NULL,
                event_id character varying(128) NOT NULL,
                event_type character varying(64) NULL,
                received_at_utc timestamp with time zone NOT NULL,
                applied boolean NOT NULL,
                CONSTRAINT PK_gateway_webhook_events PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS IX_gateway_webhook_events_intent_id
                ON gateway_webhook_events (intent_id);

            CREATE UNIQUE INDEX IF NOT EXISTS ux_gateway_webhook_events_provider_event
                ON gateway_webhook_events (provider, event_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS gateway_webhook_events;");
    }
}
