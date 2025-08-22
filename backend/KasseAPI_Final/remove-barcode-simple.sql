-- Simple Barcode Removal - No Transaction Blocks
-- Run each command separately if needed

-- 1. Remove barcode column
ALTER TABLE products DROP COLUMN IF EXISTS barcode;

-- 2. Remove barcode indexes
DROP INDEX IF EXISTS "UQ_products_barcode";
DROP INDEX IF EXISTS "IX_products_barcode";

-- 3. Show current table structure
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;
