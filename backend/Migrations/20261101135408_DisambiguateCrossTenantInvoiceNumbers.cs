using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class DisambiguateCrossTenantInvoiceNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "invoices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            // Demo seeds gave every mandant RegisterNumber KASSE-001. Rename the copies that
            // have never signed a receipt. Slug "dev" keeps KASSE-001. Registers that already
            // have payment_details keep their Kassen-ID; new BelegNr values carry the tenant id.
            migrationBuilder.Sql(
                """
                UPDATE cash_registers AS cr
                SET "RegisterNumber" = assigned.new_number,
                    updated_at = NOW()
                FROM (
                    SELECT cr2.id,
                           CASE
                               WHEN length(compact.slug_key) BETWEEN 1 AND 18 THEN 'K-' || compact.slug_key
                               ELSE 'K-' || upper(substr(replace(cr2.tenant_id::text, '-', ''), 1, 18))
                           END AS new_number
                    FROM cash_registers AS cr2
                    INNER JOIN tenants AS t ON t.id = cr2.tenant_id
                    CROSS JOIN LATERAL (
                        SELECT regexp_replace(upper(coalesce(t."Slug", '')), '[^A-Z0-9-]', '', 'g') AS slug_key
                    ) AS compact
                    WHERE lower(coalesce(t."Slug", '')) <> 'dev'
                      AND NOT EXISTS (
                          SELECT 1 FROM payment_details AS p WHERE p.cash_register_id = cr2.id
                      )
                      AND EXISTS (
                          SELECT 1
                          FROM cash_registers AS other
                          WHERE other."RegisterNumber" = cr2."RegisterNumber"
                            AND other.tenant_id <> cr2.tenant_id
                      )
                ) AS assigned
                WHERE cr.id = assigned.id
                  AND cr."RegisterNumber" IS DISTINCT FROM assigned.new_number
                  AND NOT EXISTS (
                      SELECT 1
                      FROM cash_registers AS taken
                      WHERE taken.tenant_id = cr.tenant_id
                        AND taken.id <> cr.id
                        AND taken."RegisterNumber" = assigned.new_number
                  );

                UPDATE monatsbeleg_auto_runs
                SET status = 'Failed',
                    attempt_count = 0,
                    next_retry_utc = NULL,
                    last_error = left(
                        'Reset so Auto-Monatsbeleg can retry after the invoice-number collision fix. ' || coalesce(last_error, ''),
                        500),
                    updated_at_utc = NOW()
                WHERE payment_id IS NULL
                  AND status IN ('Failed', 'Exhausted', 'Pending')
                  AND (
                      last_error ILIKE '%23505%'
                      OR last_error ILIKE '%duplicate key%'
                      OR last_error ILIKE '%ix_invoices_invoice_number%'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);
        }
    }
}
