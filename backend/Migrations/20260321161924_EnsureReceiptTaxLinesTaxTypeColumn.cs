using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class EnsureReceiptTaxLinesTaxTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy DBs may have receipt_tax_lines without tax_type; PaymentService inserts require it (TaxType enum int).
            migrationBuilder.Sql(
                """
                ALTER TABLE receipt_tax_lines ADD COLUMN IF NOT EXISTS tax_type integer NOT NULL DEFAULT 1;
                ALTER TABLE receipt_tax_lines ALTER COLUMN tax_type DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE receipt_tax_lines DROP COLUMN IF EXISTS tax_type;");
        }
    }
}
