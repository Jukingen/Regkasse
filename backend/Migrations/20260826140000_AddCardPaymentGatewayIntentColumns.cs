using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Expand <c>card_payment_transactions</c> with hosted-checkout intent columns.
/// Persistence remains this table (later renamed to <c>gateway_payment_intents</c>).
/// Does not create <c>online_payments</c>. Idempotent for DBs that already have the columns.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260826140000_AddCardPaymentGatewayIntentColumns")]
public partial class AddCardPaymentGatewayIntentColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $mig$
            BEGIN
              IF to_regclass('public.card_payment_transactions') IS NULL THEN
                RETURN;
              END IF;

              ALTER TABLE card_payment_transactions
                ADD COLUMN IF NOT EXISTS idempotency_key character varying(64) NULL,
                ADD COLUMN IF NOT EXISTS redirect_url character varying(500) NULL,
                ADD COLUMN IF NOT EXISTS return_url character varying(500) NULL,
                ADD COLUMN IF NOT EXISTS method_code character varying(32) NULL,
                ADD COLUMN IF NOT EXISTS last_webhook_event_id character varying(128) NULL,
                ADD COLUMN IF NOT EXISTS expires_at_utc timestamp with time zone NULL;

              CREATE INDEX IF NOT EXISTS IX_card_payment_transactions_gateway_payment_intent_id
                ON card_payment_transactions (gateway_payment_intent_id);

              CREATE UNIQUE INDEX IF NOT EXISTS ux_card_payment_transactions_tenant_idempotency
                ON card_payment_transactions (tenant_id, idempotency_key)
                WHERE idempotency_key IS NOT NULL;
            END
            $mig$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $mig$
            BEGIN
              IF to_regclass('public.card_payment_transactions') IS NULL THEN
                RETURN;
              END IF;

              DROP INDEX IF EXISTS ux_card_payment_transactions_tenant_idempotency;
              DROP INDEX IF EXISTS IX_card_payment_transactions_gateway_payment_intent_id;
              ALTER TABLE card_payment_transactions
                DROP COLUMN IF EXISTS expires_at_utc,
                DROP COLUMN IF EXISTS last_webhook_event_id,
                DROP COLUMN IF EXISTS method_code,
                DROP COLUMN IF EXISTS return_url,
                DROP COLUMN IF EXISTS redirect_url,
                DROP COLUMN IF EXISTS idempotency_key;
            END
            $mig$;
            """);
    }
}
