using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingProductFiscalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns exist in the EF model snapshot but were never created in the database.
            // Using IF NOT EXISTS for idempotency.
            migrationBuilder.Sql(@"
                ALTER TABLE products ADD COLUMN IF NOT EXISTS fiscal_category_code character varying(10);
                ALTER TABLE products ADD COLUMN IF NOT EXISTS is_fiscal_compliant boolean NOT NULL DEFAULT true;
                ALTER TABLE products ADD COLUMN IF NOT EXISTS is_taxable boolean NOT NULL DEFAULT true;
                ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_exemption_reason character varying(100);
                ALTER TABLE products ADD COLUMN IF NOT EXISTS rksv_product_type character varying(50) NOT NULL DEFAULT 'Standard';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products DROP COLUMN IF EXISTS fiscal_category_code;
                ALTER TABLE products DROP COLUMN IF EXISTS is_fiscal_compliant;
                ALTER TABLE products DROP COLUMN IF EXISTS is_taxable;
                ALTER TABLE products DROP COLUMN IF EXISTS tax_exemption_reason;
                ALTER TABLE products DROP COLUMN IF EXISTS rksv_product_type;
            ");
        }
    }
}
