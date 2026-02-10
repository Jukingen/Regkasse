-- PaymentDetails tablosuna frontend için gerekli alanları ekle
-- Avusturya yasal gereksinimleri (RKSV & DSGVO) için gerekli alanlar

-- Mevcut alanları kontrol et ve gerekirse ekle
DO $$
BEGIN
    -- TableNumber alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'TableNumber') THEN
        ALTER TABLE payment_details ADD COLUMN "TableNumber" INTEGER NOT NULL DEFAULT 1;
    END IF;
    
    -- CashierId alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'CashierId') THEN
        ALTER TABLE payment_details ADD COLUMN "CashierId" VARCHAR(100) NOT NULL DEFAULT '';
    END IF;
    
    -- Steuernummer alanı (ATU12345678 formatı)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'Steuernummer') THEN
        ALTER TABLE payment_details ADD COLUMN "Steuernummer" VARCHAR(12) NOT NULL DEFAULT 'ATU12345678';
    END IF;
    
    -- KassenId alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'KassenId') THEN
        ALTER TABLE payment_details ADD COLUMN "KassenId" VARCHAR(50) NOT NULL DEFAULT 'KASSE-001';
    END IF;
    
    -- TseSignature alanı (RKSV §6 zorunlu)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'TseSignature') THEN
        ALTER TABLE payment_details ADD COLUMN "TseSignature" VARCHAR(500) NOT NULL DEFAULT '';
    END IF;
    
    -- TseTimestamp alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'TseTimestamp') THEN
        ALTER TABLE payment_details ADD COLUMN "TseTimestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
    END IF;
    
    -- TaxDetails JSONB alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'TaxDetails') THEN
        ALTER TABLE payment_details ADD COLUMN "TaxDetails" JSONB NOT NULL DEFAULT '{}';
    END IF;
    
    -- PaymentItems JSONB alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'PaymentItems') THEN
        ALTER TABLE payment_details ADD COLUMN "PaymentItems" JSONB NOT NULL DEFAULT '[]';
    END IF;
    
    -- ReceiptNumber alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'ReceiptNumber') THEN
        ALTER TABLE payment_details ADD COLUMN "ReceiptNumber" VARCHAR(50) NOT NULL DEFAULT '';
    END IF;
    
    -- IsPrinted alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'IsPrinted') THEN
        ALTER TABLE payment_details ADD COLUMN "IsPrinted" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
    
    -- IsRefund alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'IsRefund') THEN
        ALTER TABLE payment_details ADD COLUMN "IsRefund" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
    
    -- OriginalPaymentId alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'OriginalPaymentId') THEN
        ALTER TABLE payment_details ADD COLUMN "OriginalPaymentId" UUID;
    END IF;
    
    -- CancellationReason alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'CancellationReason') THEN
        ALTER TABLE payment_details ADD COLUMN "CancellationReason" VARCHAR(500);
    END IF;
    
    -- CancelledAt alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'CancelledAt') THEN
        ALTER TABLE payment_details ADD COLUMN "CancelledAt" TIMESTAMP WITH TIME ZONE;
    END IF;
    
    -- RefundReason alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'RefundReason') THEN
        ALTER TABLE payment_details ADD COLUMN "RefundReason" VARCHAR(500);
    END IF;
    
    -- RefundAmount alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'RefundAmount') THEN
        ALTER TABLE payment_details ADD COLUMN "RefundAmount" DECIMAL(18,2);
    END IF;
    
    -- IsFinanzOnlineSent alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'IsFinanzOnlineSent') THEN
        ALTER TABLE payment_details ADD COLUMN "IsFinanzOnlineSent" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
    
    -- FinanzOnlineSentAt alanı
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'payment_details' AND column_name = 'FinanzOnlineSentAt') THEN
        ALTER TABLE payment_details ADD COLUMN "FinanzOnlineSentAt" TIMESTAMP WITH TIME ZONE;
    END IF;
    
END $$;

-- İndeksler oluştur
CREATE INDEX IF NOT EXISTS "IX_payment_details_tse_signature" ON "payment_details" ("TseSignature");
CREATE INDEX IF NOT EXISTS "IX_payment_details_receipt_number" ON "payment_details" ("ReceiptNumber");
CREATE INDEX IF NOT EXISTS "IX_payment_details_steuernummer" ON "payment_details" ("Steuernummer");
CREATE INDEX IF NOT EXISTS "IX_payment_details_kassen_id" ON "payment_details" ("KassenId");

-- Mevcut kayıtları güncelle
UPDATE payment_details 
SET 
    "TableNumber" = 1,
    "CashierId" = 'demo-cashier-001',
    "Steuernummer" = 'ATU12345678',
    "KassenId" = 'KASSE-001',
    "TseSignature" = 'DEMO-TSE-SIGNATURE-' || id::text,
    "TseTimestamp" = "CreatedAt",
    "ReceiptNumber" = 'AT-KASSE-' || TO_CHAR("CreatedAt", 'YYYYMMDD') || '-' || id::text
WHERE "TseSignature" = '' OR "TseSignature" IS NULL;

-- Log mesajı
DO $$
BEGIN
    RAISE NOTICE 'PaymentDetails tablosu başarıyla güncellendi. Frontend için gerekli tüm alanlar eklendi.';
END $$;
