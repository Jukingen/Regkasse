-- PaymentSession tablosuna iptal işlemi için gerekli alanları ekle
-- Türkçe Açıklama: Bu script PaymentSession tablosuna ödeme iptal işlemi için gerekli alanları ekler

ALTER TABLE "PaymentSessions" 
ADD COLUMN "CancelledAt" timestamp with time zone NULL,
ADD COLUMN "CancelledBy" character varying(100) NULL,
ADD COLUMN "CancellationReason" character varying(500) NULL;

-- Yeni alanlar için açıklama ekle
COMMENT ON COLUMN "PaymentSessions"."CancelledAt" IS 'Ödeme iptal edildiği zaman';
COMMENT ON COLUMN "PaymentSessions"."CancelledBy" IS 'Ödemeyi iptal eden kullanıcı ID';
COMMENT ON COLUMN "PaymentSessions"."CancellationReason" IS 'Ödeme iptal sebebi';

-- İndeks ekle (opsiyonel)
CREATE INDEX IF NOT EXISTS "IX_PaymentSessions_CancelledAt" ON "PaymentSessions" ("CancelledAt");
CREATE INDEX IF NOT EXISTS "IX_PaymentSessions_CancelledBy" ON "PaymentSessions" ("CancelledBy");
