using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Databases that applied <c>ix_payment_details_one_storno_per_original</c> before
    /// <c>is_active</c> was part of the predicate still have the old filter.
    /// Drop and recreate so the live index matches <c>AppDbContext</c>.
    /// </summary>
    public partial class RebuildPaymentStornoUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_payment_details_one_storno_per_original;
                CREATE UNIQUE INDEX ix_payment_details_one_storno_per_original
                    ON payment_details ("OriginalPaymentId")
                    WHERE "IsStorno" = true AND "OriginalPaymentId" IS NOT NULL AND is_active = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_payment_details_one_storno_per_original;
                CREATE UNIQUE INDEX ix_payment_details_one_storno_per_original
                    ON payment_details ("OriginalPaymentId")
                    WHERE "IsStorno" = true;
                """);
        }
    }
}
