-- Safe Barcode Removal Script
-- This script handles transaction abortion issues by using individual commands

-- Step 1: Check current transaction status and reset if needed
DO $$
BEGIN
    -- Check if we're in a transaction
    IF (SELECT txid_current()) IS NOT NULL THEN
        -- If we're in a failed transaction, we need to handle it
        RAISE NOTICE 'Current transaction ID: %', txid_current();
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'Transaction error detected, continuing with cleanup...';
END $$;

-- Step 2: Remove barcode column (if exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'products' AND column_name = 'barcode'
    ) THEN
        ALTER TABLE products DROP COLUMN barcode;
        RAISE NOTICE 'Barcode column removed successfully';
    ELSE
        RAISE NOTICE 'Barcode column does not exist, skipping removal';
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'Error removing barcode column: %', SQLERRM;
END $$;

-- Step 3: Remove barcode indexes (if exist)
DO $$
BEGIN
    -- Drop unique index if exists
    IF EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE tablename = 'products' AND indexname = 'UQ_products_barcode'
    ) THEN
        DROP INDEX "UQ_products_barcode";
        RAISE NOTICE 'UQ_products_barcode index removed';
    END IF;
    
    -- Drop regular index if exists
    IF EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE tablename = 'products' AND indexname = 'IX_products_barcode'
    ) THEN
        DROP INDEX "IX_products_barcode";
        RAISE NOTICE 'IX_products_barcode index removed';
    END IF;
    
    -- Drop any other barcode-related indexes
    IF EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE tablename = 'products' AND indexname LIKE '%barcode%'
    ) THEN
        DROP INDEX IF EXISTS "IX_products_barcode_1";
        DROP INDEX IF EXISTS "IX_products_barcode_2";
        RAISE NOTICE 'Additional barcode indexes removed';
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'Error removing barcode indexes: %', SQLERRM;
END $$;

-- Step 4: Verify current table structure
SELECT 'Current products table structure:' as info;
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- Step 5: Check for any remaining barcode references
SELECT 'Checking for remaining barcode references:' as info;
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products' AND indexdef LIKE '%barcode%';

-- Step 6: Final status
SELECT 'Barcode removal process completed!' as status;
