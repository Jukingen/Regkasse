using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Renames <c>card_payment_transactions</c> to <c>gateway_payment_intents</c> (single SoT for
/// card/PayPal intents). Does not create <c>online_payments</c> or webhook-event rows.
/// Idempotent if the rename already happened.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260826160000_RenameToGatewayPaymentIntents")]
public partial class RenameToGatewayPaymentIntents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $mig$
            BEGIN
              IF to_regclass('public.card_payment_transactions') IS NOT NULL
                 AND to_regclass('public.gateway_payment_intents') IS NULL THEN
                ALTER TABLE card_payment_transactions RENAME TO gateway_payment_intents;
              END IF;

              IF to_regclass('public.gateway_payment_intents') IS NULL THEN
                RETURN;
              END IF;

              ALTER INDEX IF EXISTS IX_card_payment_transactions_cash_register_id
                RENAME TO IX_gateway_payment_intents_cash_register_id;
              ALTER INDEX IF EXISTS IX_card_payment_transactions_created_at
                RENAME TO IX_gateway_payment_intents_created_at;
              ALTER INDEX IF EXISTS IX_card_payment_transactions_payment_id
                RENAME TO IX_gateway_payment_intents_payment_id;
              ALTER INDEX IF EXISTS IX_card_payment_transactions_status
                RENAME TO IX_gateway_payment_intents_status;
              ALTER INDEX IF EXISTS IX_card_payment_transactions_tenant_id
                RENAME TO IX_gateway_payment_intents_tenant_id;
              ALTER INDEX IF EXISTS IX_card_payment_transactions_gateway_payment_intent_id
                RENAME TO IX_gateway_payment_intents_gateway_payment_intent_id;
              ALTER INDEX IF EXISTS ux_card_payment_transactions_tenant_idempotency
                RENAME TO ux_gateway_payment_intents_tenant_idempotency;

              ALTER TABLE gateway_payment_intents
                ADD COLUMN IF NOT EXISTS capture_mode character varying(32) NOT NULL DEFAULT 'automatic',
                ADD COLUMN IF NOT EXISTS cart_snapshot_id uuid NULL;
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
              IF to_regclass('public.gateway_payment_intents') IS NULL THEN
                RETURN;
              END IF;

              ALTER TABLE gateway_payment_intents
                DROP COLUMN IF EXISTS cart_snapshot_id,
                DROP COLUMN IF EXISTS capture_mode;

              ALTER INDEX IF EXISTS ux_gateway_payment_intents_tenant_idempotency
                RENAME TO ux_card_payment_transactions_tenant_idempotency;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_gateway_payment_intent_id
                RENAME TO IX_card_payment_transactions_gateway_payment_intent_id;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_tenant_id
                RENAME TO IX_card_payment_transactions_tenant_id;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_status
                RENAME TO IX_card_payment_transactions_status;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_payment_id
                RENAME TO IX_card_payment_transactions_payment_id;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_created_at
                RENAME TO IX_card_payment_transactions_created_at;
              ALTER INDEX IF EXISTS IX_gateway_payment_intents_cash_register_id
                RENAME TO IX_card_payment_transactions_cash_register_id;

              IF to_regclass('public.card_payment_transactions') IS NULL THEN
                ALTER TABLE gateway_payment_intents RENAME TO card_payment_transactions;
              END IF;
            END
            $mig$;
            """);
    }
}
