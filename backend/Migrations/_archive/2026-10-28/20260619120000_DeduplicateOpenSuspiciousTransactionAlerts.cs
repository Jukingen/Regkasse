using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Data migration: legacy duplicate open rows in <c>suspicious_transaction_alerts</c> that share
    /// the same <c>(tenant_id, dedup_key)</c>. Keeps the newest open alert per group; acknowledges
    /// older siblings (status Open → Acknowledged). Irreversible — original open state is not restored.
    /// </summary>
    public partial class DeduplicateOpenSuspiciousTransactionAlerts : Migration
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
                            PARTITION BY tenant_id, dedup_key
                            ORDER BY detected_at_utc DESC, created_at DESC, id DESC
                        ) AS rn
                    FROM suspicious_transaction_alerts
                    WHERE status = 1
                      AND is_active = true
                )
                UPDATE suspicious_transaction_alerts AS a
                SET
                    status = 2,
                    updated_at = NOW(),
                    updated_by = 'migration:DeduplicateOpenSuspiciousTransactionAlerts'
                FROM ranked AS r
                WHERE a.id = r.id
                  AND r.rn > 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; acknowledged duplicates cannot be inferred reliably.
        }
    }
}
