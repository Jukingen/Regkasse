-- =====================================================
-- MASA BAZLI SEPET YÖNETİMİ VERİTABANI TEMİZLİK SCRIPT'İ
-- =====================================================
-- Bu script masa bazlı sepet yönetimi için veritabanını temizler ve optimize eder
-- Çalıştırmadan önce veritabanı yedeği alın!

-- 🧹 1. TableNumber olmayan veya geçersiz eski sepetleri temizle
BEGIN TRANSACTION;

-- Önce CartItems tablosundan eski verileri temizle
DELETE FROM "CartItems" 
WHERE "CartId" IN (
    SELECT c."CartId" 
    FROM "Carts" c 
    WHERE c."TableNumber" IS NULL 
       OR c."TableNumber" <= 0
       OR c."TableNumber" > 100
);

-- Sonra Carts tablosundan eski verileri temizle
DELETE FROM "Carts" 
WHERE "TableNumber" IS NULL 
   OR "TableNumber" <= 0
   OR "TableNumber" > 100;

-- 🔒 2. TableNumber alanını NOT NULL yap ve constraint ekle
ALTER TABLE "Carts" ALTER COLUMN "TableNumber" SET NOT NULL;

-- TableNumber için check constraint ekle (1-100 arası)
ALTER TABLE "Carts" ADD CONSTRAINT "CK_Carts_TableNumber_Range" 
CHECK ("TableNumber" >= 1 AND "TableNumber" <= 100);

-- 📊 3. Masa bazlı performans index'leri ekle
CREATE INDEX IF NOT EXISTS "IX_Carts_TableNumber_Status_UserId" 
ON "Carts" ("TableNumber", "Status", "UserId");

CREATE INDEX IF NOT EXISTS "IX_Carts_TableNumber_Status" 
ON "Carts" ("TableNumber", "Status");

CREATE INDEX IF NOT EXISTS "IX_Carts_TableNumber_CreatedAt" 
ON "Carts" ("TableNumber", "CreatedAt");

-- 🚫 4. Masa bazlı unique constraint ekle (aynı masada birden fazla aktif sepet olmamalı)
-- Önce mevcut duplicate'ları temizle (en eski olanı tut, diğerlerini sil)
DELETE FROM "Carts" 
WHERE "Id" IN (
    SELECT c2."Id" 
    FROM "Carts" c1
    INNER JOIN "Carts" c2 ON c1."TableNumber" = c2."TableNumber" 
        AND c1."Status" = c2."Status" 
        AND c1."Status" = 1  -- Active
        AND c1."Id" < c2."Id"
);

-- Unique constraint ekle (TableNumber + Status + UserId kombinasyonu)
ALTER TABLE "Carts" ADD CONSTRAINT "UQ_Carts_TableNumber_Status_UserId" 
UNIQUE ("TableNumber", "Status", "UserId");

-- 🔄 5. Masa bazlı sepet durumu güncelleme
UPDATE "Carts" 
SET "Status" = 4  -- Expired
WHERE "Status" = 1  -- Active
  AND "ExpiresAt" < NOW() - INTERVAL '24 hours';

-- 📝 6. Log tablosu için masa bazlı index (eğer varsa)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Logs') THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS IX_Logs_TableNumber_CreatedAt ON "Logs" ("TableNumber", "CreatedAt")';
    END IF;
END $$;

-- 🎯 7. Masa bazlı sepet istatistikleri için view oluştur
CREATE OR REPLACE VIEW "TableCartStatistics" AS
SELECT 
    c."TableNumber",
    COUNT(CASE WHEN c."Status" = 1 THEN 1 END) as ActiveCarts,
    COUNT(CASE WHEN c."Status" = 2 THEN 1 END) as CompletedCarts,
    COUNT(CASE WHEN c."Status" = 3 THEN 1 END) as CancelledCarts,
    COUNT(CASE WHEN c."Status" = 4 THEN 1 END) as ExpiredCarts,
    SUM(CASE WHEN c."Status" = 1 THEN ci."Quantity" ELSE 0 END) as TotalActiveItems,
    MAX(c."CreatedAt") as LastCartActivity
FROM "Carts" c
LEFT JOIN "CartItems" ci ON c."CartId" = ci."CartId"
GROUP BY c."TableNumber"
ORDER BY c."TableNumber";

COMMIT;

-- =====================================================
-- TEMİZLİK SONRASI KONTROL SORGULARI
-- =====================================================

-- 📊 Masa bazlı sepet durumu özeti
SELECT 
    "TableNumber",
    COUNT(*) as TotalCarts,
    COUNT(CASE WHEN "Status" = 1 THEN 1 END) as ActiveCarts,
    COUNT(CASE WHEN "Status" = 2 THEN 1 END) as CompletedCarts,
    COUNT(CASE WHEN "Status" = 3 THEN 1 END) as CancelledCarts,
    COUNT(CASE WHEN "Status" = 4 THEN 1 END) as ExpiredCarts
FROM "Carts"
GROUP BY "TableNumber"
ORDER BY "TableNumber";

-- 🔍 TableNumber constraint kontrolü
SELECT 
    "TableNumber",
    COUNT(*) as CartCount
FROM "Carts"
WHERE "TableNumber" < 1 OR "TableNumber" > 100
GROUP BY "TableNumber";

-- 📈 Masa bazlı sepet istatistikleri
SELECT * FROM "TableCartStatistics";

-- ⚠️ Uyarı: Eğer yukarıdaki sorgularda hata varsa, ROLLBACK yapın
-- ROLLBACK;
