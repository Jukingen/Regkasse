using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixProductSchemaComprehensiveFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Manual SQL Reconciliation - Comprehensive and Idempotent
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    -- 1. Drop existing constraints if they exist (to avoid errors during rename or add)
                    IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'CK_products_price_positive') THEN
                        ALTER TABLE products DROP CONSTRAINT CK_products_price_positive;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'CK_products_stock_quantity_non_negative') THEN
                        ALTER TABLE products DROP CONSTRAINT CK_products_stock_quantity_non_negative;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'CK_products_min_stock_level_non_negative') THEN
                        ALTER TABLE products DROP CONSTRAINT CK_products_min_stock_level_non_negative;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'CK_products_cost_non_negative') THEN
                        ALTER TABLE products DROP CONSTRAINT CK_products_cost_non_negative;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'CK_products_tax_rate_range') THEN
                        ALTER TABLE products DROP CONSTRAINT CK_products_tax_rate_range;
                    END IF;

                    -- 2. Rename existing PascalCase columns to lowercase if they exist
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'Price') THEN
                        ALTER TABLE products RENAME COLUMN ""Price"" TO price;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'TaxType') THEN
                        ALTER TABLE products RENAME COLUMN ""TaxType"" TO tax_type;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'TaxRate') THEN
                        ALTER TABLE products RENAME COLUMN ""TaxRate"" TO tax_rate;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'StockQuantity') THEN
                        ALTER TABLE products RENAME COLUMN ""StockQuantity"" TO stock_quantity;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'MinStockLevel') THEN
                        ALTER TABLE products RENAME COLUMN ""MinStockLevel"" TO min_stock_level;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'Unit') THEN
                        ALTER TABLE products RENAME COLUMN ""Unit"" TO unit;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'ImageUrl') THEN
                        ALTER TABLE products RENAME COLUMN ""ImageUrl"" TO image_url;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'IsActive') THEN
                        ALTER TABLE products RENAME COLUMN ""IsActive"" TO is_active;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'CreatedAt') THEN
                        ALTER TABLE products RENAME COLUMN ""CreatedAt"" TO created_at;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'UpdatedAt') THEN
                        ALTER TABLE products RENAME COLUMN ""UpdatedAt"" TO updated_at;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'CreatedBy') THEN
                        ALTER TABLE products RENAME COLUMN ""CreatedBy"" TO created_by;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'UpdatedBy') THEN
                        ALTER TABLE products RENAME COLUMN ""UpdatedBy"" TO updated_by;
                    END IF;

                    -- 3. Add missing columns with correct types and defaults (snake_case)
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS name character varying(200) NOT NULL DEFAULT '';
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS price decimal(18,2) NOT NULL DEFAULT 0;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_type character varying(20) NOT NULL DEFAULT 'Standard';
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS description text;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS category character varying(100) NOT NULL DEFAULT '';
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS image_url character varying(500);
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS stock_quantity integer NOT NULL DEFAULT 0;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS min_stock_level integer NOT NULL DEFAULT 0;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS unit character varying(20) NOT NULL DEFAULT '';
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS cost decimal(18,2) NOT NULL DEFAULT 0;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_rate decimal(5,2) NOT NULL DEFAULT 0;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS is_fiscal_compliant boolean NOT NULL DEFAULT true;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS fiscal_category_code character varying(10);
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS is_taxable boolean NOT NULL DEFAULT true;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_exemption_reason character varying(100);
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS rksv_product_type character varying(50) NOT NULL DEFAULT 'Standard';
                    
                    -- BaseEntity columns
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT now();
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS created_by character varying(450);
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS updated_by character varying(450);
                    ALTER TABLE products ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT true;

                    -- 4. Re-apply constraints using lowercase identifiers
                    ALTER TABLE products ADD CONSTRAINT CK_products_price_positive CHECK (price >= 0);
                    ALTER TABLE products ADD CONSTRAINT CK_products_stock_quantity_non_negative CHECK (stock_quantity >= 0);
                    ALTER TABLE products ADD CONSTRAINT CK_products_min_stock_level_non_negative CHECK (min_stock_level >= 0);
                    ALTER TABLE products ADD CONSTRAINT CK_products_cost_non_negative CHECK (cost >= 0);
                    ALTER TABLE products ADD CONSTRAINT CK_products_tax_rate_range CHECK (tax_rate >= 0 AND tax_rate <= 100);
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Manual reconciliation Down is complex, keeping basic
        }
    }
}
