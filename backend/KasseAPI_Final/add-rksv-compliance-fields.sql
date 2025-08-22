-- Add RKSV Compliance Fields to Products Table
-- This script adds Austrian fiscal compliance fields for RKSV standards

-- Step 1: Add RKSV compliance fields
ALTER TABLE products 
ADD COLUMN IF NOT EXISTS is_fiscal_compliant BOOLEAN DEFAULT true,
ADD COLUMN IF NOT EXISTS fiscal_category_code VARCHAR(10),
ADD COLUMN IF NOT EXISTS is_taxable BOOLEAN DEFAULT true,
ADD COLUMN IF NOT EXISTS tax_exemption_reason VARCHAR(100),
ADD COLUMN IF NOT EXISTS rksv_product_type VARCHAR(50) DEFAULT 'Standard';

-- Step 2: Update existing products to be RKSV compliant
UPDATE products 
SET 
    is_fiscal_compliant = true,
    is_taxable = true,
    rksv_product_type = CASE 
        WHEN tax_type = 'Standard' THEN 'Standard'
        WHEN tax_type = 'Reduced' THEN 'Reduced'
        WHEN tax_type = 'Special' THEN 'Special'
        ELSE 'Standard'
    END
WHERE is_fiscal_compliant IS NULL OR is_taxable IS NULL OR rksv_product_type IS NULL;

-- Step 3: Add comments for documentation
COMMENT ON COLUMN products.is_fiscal_compliant IS 'RKSV fiscal compliance flag for Austrian standards';
COMMENT ON COLUMN products.fiscal_category_code IS 'Austrian fiscal category code for tax classification';
COMMENT ON COLUMN products.is_taxable IS 'Flag indicating if product is subject to Austrian VAT';
COMMENT ON COLUMN products.tax_exemption_reason IS 'Reason for tax exemption if applicable';
COMMENT ON COLUMN products.rksv_product_type IS 'RKSV product type classification (Standard, Reduced, Special, Exempt, Service, Digital)';

-- Step 4: Verify the changes
SELECT 'RKSV compliance fields added successfully!' as status;

-- Step 5: Show current table structure
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;
