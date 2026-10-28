using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Idempotent guard: ensures receipt sequence and signature chain unique indexes exist
    /// (e.g. manual DDL drift, partial apply, or restored DB). Fails if duplicate rows prevent uniqueness.
    /// Run scripts/sql/fiscal_go_live_validation.sql on a copy before production migrate if unsure.
    /// </summary>
    public partial class EnsureFiscalSequenceAndChainUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_receipt_sequences_cash_register_id_sequence_date"
                ON receipt_sequences (cash_register_id, sequence_date);
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_signature_chain_state_cash_register_id"
                ON signature_chain_state (cash_register_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: dropping these indexes would weaken fiscal concurrency guarantees.
        }
    }
}
