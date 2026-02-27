using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeTaxTypeAndBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. table_order_items: cast TaxType to integer
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'table_order_items' AND column_name = 'TaxType') THEN
                        ALTER TABLE table_order_items ALTER COLUMN ""TaxType"" TYPE integer USING ""TaxType""::integer;
                    END IF;
                END $$;");

            // 2. products: cast tax_type to integer and add barcode
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    -- tax_type cast
                    IF EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'tax_type') THEN
                        ALTER TABLE products ALTER COLUMN tax_type TYPE integer USING tax_type::integer;
                    END IF;
                    
                    -- barcode column
                    IF NOT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'barcode') THEN
                        ALTER TABLE products ADD COLUMN barcode varchar(50) NOT NULL DEFAULT '';
                    END IF;
                END $$;");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_products_barcode\" ON products(barcode)");

            // 3. payment_items: handle missing table or column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'payment_items') THEN
                        IF EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'payment_items' AND column_name = 'TaxType') THEN
                            ALTER TABLE payment_items ALTER COLUMN ""TaxType"" TYPE integer USING ""TaxType""::integer;
                        END IF;
                    END IF;
                END $$;");

            // Commented out EF-generated methods to prevent duplicate/failing SQL execution
            /*
            migrationBuilder.AlterColumn<int>(
                name: "TaxType",
                table: "table_order_items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "tax_type",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "TaxType",
                table: "payment_items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true);
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration not strictly necessary for this stabilization but kept for structure
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_products_barcode\"");
            
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'barcode') THEN
                        ALTER TABLE products DROP COLUMN barcode;
                    END IF;
                END $$;");
        }
    }
}
