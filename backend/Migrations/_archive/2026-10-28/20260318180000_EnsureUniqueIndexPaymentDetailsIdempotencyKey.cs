using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Ensures a unique index on payment_details.idempotency_key so duplicate keys cannot be inserted.
    /// Idempotent: creates the index only if it does not exist (e.g. after AddPaymentDetailsIdempotencyKey).
    /// </summary>
    public partial class EnsureUniqueIndexPaymentDetailsIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure column exists (defensive; AddPaymentDetailsIdempotencyKey already adds it)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'idempotency_key'
                    ) THEN
                        ALTER TABLE payment_details ADD COLUMN idempotency_key character varying(64) NULL;
                    END IF;
                END $$;
            ");

            // Drop non-unique index if it exists (e.g. from an older migration that created a non-unique index)
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_payment_details_idempotency_key"";
            ");

            // Create unique index; IF NOT EXISTS so re-run is safe
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_payment_details_idempotency_key""
                ON payment_details (idempotency_key)
                WHERE idempotency_key IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_payment_details_idempotency_key"";");
        }
    }
}
