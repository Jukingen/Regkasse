using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// One storno payment per original sale. Keeps the oldest row (created_at, then id).
    /// Rows without a TSE/JWS signature and without child references are deleted.
    /// Signed rows, and unsigned rows that another table still references, stay in the table with is_active = false.
    /// </summary>
    public partial class DeduplicatePaymentStornoRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY "OriginalPaymentId"
                            ORDER BY created_at ASC, id ASC
                        ) AS rn,
                        (
                            btrim(coalesce("TseSignature", '')) <> ''
                            OR btrim(coalesce(jws_signature, '')) <> ''
                        ) AS has_fiscal_signature
                    FROM payment_details
                    WHERE "IsStorno" = true
                      AND "OriginalPaymentId" IS NOT NULL
                ),
                extras AS (
                    SELECT id
                    FROM ranked
                    WHERE rn > 1
                      AND NOT has_fiscal_signature
                )
                DELETE FROM payment_details AS p
                USING extras AS e
                WHERE p.id = e.id
                  AND NOT EXISTS (SELECT 1 FROM receipts r WHERE r.payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM voucher_ledger_entries v WHERE v.payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM rksv_special_receipt_finanz_online_submissions s WHERE s.payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM gateway_payment_intents g WHERE g.payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM offline_orders o WHERE o.synced_payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM de_tse_signatures d WHERE d.payment_details_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM product_imeis i WHERE i.sold_payment_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM ticket_redemptions t WHERE t.payment_detail_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM guest_folio_items f WHERE f.payment_detail_id = p.id);
                """);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY "OriginalPaymentId"
                            ORDER BY created_at ASC, id ASC
                        ) AS rn
                    FROM payment_details
                    WHERE "IsStorno" = true
                      AND "OriginalPaymentId" IS NOT NULL
                )
                UPDATE payment_details AS p
                SET is_active = false,
                    updated_at = NOW()
                FROM ranked AS r
                WHERE p.id = r.id
                  AND r.rn > 1
                  AND p.is_active = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deleted unsigned copies cannot be restored. Extra rows that Up left inactive
            // become active again. Rollback drops the following unique index before this runs.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY "OriginalPaymentId"
                            ORDER BY created_at ASC, id ASC
                        ) AS rn
                    FROM payment_details
                    WHERE "IsStorno" = true
                      AND "OriginalPaymentId" IS NOT NULL
                )
                UPDATE payment_details AS p
                SET is_active = true,
                    updated_at = NOW()
                FROM ranked AS r
                WHERE p.id = r.id
                  AND r.rn > 1
                  AND p.is_active = false;
                """);
        }
    }
}
