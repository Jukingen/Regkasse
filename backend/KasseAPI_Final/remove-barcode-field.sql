-- Barcode alanını products tablosundan kaldır
-- ADIM 1: Barcode alanını kaldır
SELECT 'ADIM 1: Barcode alanını kaldırılıyor...' as status;

-- Barcode alanını kaldır
ALTER TABLE products DROP COLUMN IF EXISTS barcode;

-- ADIM 2: Barcode ile ilgili index'leri kaldır
SELECT 'ADIM 2: Barcode index\'leri kaldırılıyor...' as status;

-- Barcode unique index'ini kaldır (eğer varsa)
DROP INDEX IF EXISTS "UQ_products_barcode";
DROP INDEX IF EXISTS "IX_products_barcode";

-- ADIM 3: Kontrol
SELECT 'ADIM 3: Kontrol yapılıyor...' as status;

-- Products tablosunun mevcut yapısını kontrol et
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- ADIM 4: Tamamlandı
SELECT 'ADIM 4: Barcode alanı başarıyla kaldırıldı!' as status;
