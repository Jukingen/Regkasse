-- PaymentDetails tablosuna frontend için eksik alanları ekle
-- Sadece eksik alanları kontrol edip ekle

-- Önce mevcut sütunları kontrol et
DO $$
DECLARE
    column_exists INTEGER;
BEGIN
    -- TableNumber
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'TableNumber';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "TableNumber" INTEGER NOT NULL DEFAULT 1;
        RAISE NOTICE 'TableNumber sütunu eklendi';
    END IF;
    
    -- CashierId
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'CashierId';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "CashierId" VARCHAR(100) NOT NULL DEFAULT 'demo-cashier';
        RAISE NOTICE 'CashierId sütunu eklendi';
    END IF;
    
    -- Steuernummer (Avusturya vergi numarası)
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'Steuernummer';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "Steuernummer" VARCHAR(12) NOT NULL DEFAULT 'ATU12345678';
        RAISE NOTICE 'Steuernummer sütunu eklendi';
    END IF;
    
    -- KassenId
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'KassenId';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "KassenId" VARCHAR(50) NOT NULL DEFAULT 'KASSE-001';
        RAISE NOTICE 'KassenId sütunu eklendi';
    END IF;
    
    -- TseSignature (TSE imzası - RKSV zorunlu)
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'TseSignature';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "TseSignature" VARCHAR(500) NOT NULL DEFAULT '';
        RAISE NOTICE 'TseSignature sütunu eklendi';
    END IF;
    
    -- TseTimestamp
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'TseTimestamp';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "TseTimestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
        RAISE NOTICE 'TseTimestamp sütunu eklendi';
    END IF;
    
    -- TaxDetails (JSONB)
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'TaxDetails';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "TaxDetails" JSONB NOT NULL DEFAULT '{}';
        RAISE NOTICE 'TaxDetails sütunu eklendi';
    END IF;
    
    -- PaymentItems (JSONB)
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'PaymentItems';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "PaymentItems" JSONB NOT NULL DEFAULT '[]';
        RAISE NOTICE 'PaymentItems sütunu eklendi';
    END IF;
    
    -- ReceiptNumber
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'ReceiptNumber';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "ReceiptNumber" VARCHAR(50) NOT NULL DEFAULT '';
        RAISE NOTICE 'ReceiptNumber sütunu eklendi';
    END IF;
    
    -- IsPrinted
    SELECT COUNT(*) INTO column_exists 
    FROM information_schema.columns 
    WHERE table_name = 'payment_details' AND column_name = 'IsPrinted';
    
    IF column_exists = 0 THEN
        ALTER TABLE payment_details ADD COLUMN "IsPrinted" BOOLEAN NOT NULL DEFAULT FALSE;
        RAISE NOTICE 'IsPrinted sütunu eklendi';
    END IF;
    
    RAISE NOTICE 'PaymentDetails tablosu güncelleme tamamlandı!';
END $$;

-- Performans için indeksler ekle
CREATE INDEX IF NOT EXISTS "IX_payment_details_table_number" ON "payment_details" ("TableNumber");
CREATE INDEX IF NOT EXISTS "IX_payment_details_cashier_id" ON "payment_details" ("CashierId");
CREATE INDEX IF NOT EXISTS "IX_payment_details_steuernummer" ON "payment_details" ("Steuernummer");
CREATE INDEX IF NOT EXISTS "IX_payment_details_kassen_id" ON "payment_details" ("KassenId");
CREATE INDEX IF NOT EXISTS "IX_payment_details_tse_signature" ON "payment_details" ("TseSignature");
CREATE INDEX IF NOT EXISTS "IX_payment_details_receipt_number" ON "payment_details" ("ReceiptNumber");

-- Mevcut kayıtları güncelle (eğer varsa)
UPDATE payment_details 
SET 
    "TseSignature" = CASE 
        WHEN "TseSignature" = '' OR "TseSignature" IS NULL 
        THEN 'DEMO-TSE-' || EXTRACT(EPOCH FROM NOW()) || '-' || "id"::TEXT
        ELSE "TseSignature"
    END,
    "ReceiptNumber" = CASE
        WHEN "ReceiptNumber" = '' OR "ReceiptNumber" IS NULL
        THEN 'AT-KASSE-' || TO_CHAR(NOW(), 'YYYYMMDD') || '-' || "id"::TEXT
        ELSE "ReceiptNumber"
    END
WHERE "TseSignature" = '' OR "TseSignature" IS NULL OR "ReceiptNumber" = '' OR "ReceiptNumber" IS NULL;

SELECT 'PaymentDetails tablosu başarıyla güncellendi!' as result;
