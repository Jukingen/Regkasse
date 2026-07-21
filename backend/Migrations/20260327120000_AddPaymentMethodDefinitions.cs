using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Konfigurierbare Zahlungsarten für POS + Admin; Legacy-Mapping 0–5 bleibt kompatibel mit payment_details.PaymentMethod (varchar).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260327120000_AddPaymentMethodDefinitions")]
public partial class AddPaymentMethodDefinitions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS payment_method_definitions (
                id uuid NOT NULL PRIMARY KEY,
                code character varying(64) NOT NULL,
                name character varying(128) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                is_default boolean NOT NULL DEFAULT FALSE,
                display_order integer NOT NULL DEFAULT 0,
                legacy_payment_method_value integer NOT NULL,
                fiscal_category character varying(64) NULL,
                requires_terminal boolean NOT NULL DEFAULT FALSE,
                terminal_type character varying(64) NULL,
                allow_refund boolean NOT NULL DEFAULT TRUE,
                icon character varying(64) NULL,
                metadata_json text NULL,
                created_at_utc timestamp with time zone NOT NULL,
                updated_at_utc timestamp with time zone NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_payment_method_definitions_code""
                ON payment_method_definitions (code);
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_payment_method_definitions_is_active_display_order""
                ON payment_method_definitions (is_active, display_order);
        ");

        // Seed defaults (idempotent): match prior hardcoded POS four methods + optional inactive legacy rows.
        migrationBuilder.Sql(@"
            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000001-0000-4000-8000-000000000001'::uuid, 'cash', 'Bar', TRUE, TRUE, 10,
                0, 'Cash', FALSE, NULL, TRUE, 'cash-outline', NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'cash');

            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000002-0000-4000-8000-000000000002'::uuid, 'card', 'Karte', TRUE, FALSE, 20,
                1, 'Card', TRUE, 'card', TRUE, 'card-outline', NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'card');

            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000003-0000-4000-8000-000000000003'::uuid, 'transfer', 'Überweisung', TRUE, FALSE, 30,
                2, 'BankTransfer', FALSE, NULL, TRUE, 'swap-horizontal-outline', NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'transfer');

            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000004-0000-4000-8000-000000000004'::uuid, 'voucher', 'Gutschein', TRUE, FALSE, 40,
                4, 'Voucher', FALSE, NULL, TRUE, 'ticket-outline', NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'voucher');

            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000005-0000-4000-8000-000000000005'::uuid, 'check', 'Scheck', FALSE, FALSE, 50,
                3, 'Check', FALSE, NULL, TRUE, NULL, NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'check');

            INSERT INTO payment_method_definitions (
                id, code, name, is_active, is_default, display_order,
                legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
            )
            SELECT 'a0000006-0000-4000-8000-000000000006'::uuid, 'mobile', 'Mobil', FALSE, FALSE, 60,
                5, 'Mobile', TRUE, 'softpos', TRUE, NULL, NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM payment_method_definitions WHERE code = 'mobile');
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS payment_method_definitions;");
    }
}
