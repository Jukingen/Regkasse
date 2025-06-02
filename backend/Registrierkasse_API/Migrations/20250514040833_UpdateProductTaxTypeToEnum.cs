using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Registrierkasse.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductTaxTypeToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products 
                ALTER COLUMN tax_type TYPE integer 
                USING CASE 
                    WHEN tax_type = 'standard' THEN 20
                    WHEN tax_type = 'reduced' THEN 10
                    WHEN tax_type = 'special' THEN 13
                    ELSE 20
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE products 
                ALTER COLUMN tax_type TYPE text 
                USING CASE 
                    WHEN tax_type = 20 THEN 'standard'
                    WHEN tax_type = 10 THEN 'reduced'
                    WHEN tax_type = 13 THEN 'special'
                    ELSE 'standard'
                END;
            ");
        }
    }
}
