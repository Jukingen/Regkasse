-- =====================================================
-- PRODUCT TABLOSU RKSV UYUMLU GÜNCELLEME SCRIPTI
-- Avusturya kasa sistemi standartlarına uygun hale getirme
-- =====================================================

-- Mevcut tabloyu yedekle (isteğe bağlı)
-- CREATE TABLE products_backup AS SELECT * FROM products;

-- 1. MEVCUT ALANLARI GÜNCELLE (sadece gerekli olanlar)
BEGIN TRANSACTION;

ALTER TABLE products 
ALTER COLUMN tax_type TYPE VARCHAR(20),
ALTER COLUMN category TYPE VARCHAR(100),
ALTER COLUMN barcode TYPE VARCHAR(100),
ALTER COLUMN name TYPE VARCHAR(200),
ALTER COLUMN description TYPE TEXT;

-- 2. YENİ INDEX'LER EKLE (sadece gerekli olanlar)
CREATE INDEX IF NOT EXISTS IX_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_products_category ON products(category);
CREATE INDEX IF NOT EXISTS IX_products_tax_type ON products(tax_type);

-- 3. UNIQUE CONSTRAINT EKLE
-- Barkod benzersiz olmalı (null değerler hariç)
CREATE UNIQUE INDEX IF NOT EXISTS UQ_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;

-- 4. CHECK CONSTRAINT'LER EKLE (sadece gerekli olanlar)
-- Fiyat pozitif olmalı
ALTER TABLE products ADD CONSTRAINT CK_products_price_positive CHECK (price >= 0);

-- Stok miktarı negatif olamaz
ALTER TABLE products ADD CONSTRAINT CK_products_stock_quantity_non_negative CHECK (stock_quantity >= 0);

-- Minimum stok seviyesi negatif olamaz
ALTER TABLE products ADD CONSTRAINT CK_products_min_stock_level_non_negative CHECK (min_stock_level >= 0);

-- 5. YORUMLAR EKLE (PostgreSQL syntax)
COMMENT ON TABLE products IS 'RKSV uyumlu ürün tablosu - Avusturya kasa sistemi standartlarına uygun';
COMMENT ON COLUMN products.tax_type IS 'RKSV vergi tipi: Standard(20%), Reduced(10%), Special(13%)';
COMMENT ON COLUMN products.category IS 'Ürün kategorisi';
COMMENT ON COLUMN products.barcode IS 'Barkod (EAN-13, UPC, vb.)';
COMMENT ON COLUMN products.name IS 'Ürün adı';
COMMENT ON COLUMN products.description IS 'Ürün açıklaması';

-- 6. MEVCUT VERİLERİ GÜNCELLE (isteğe bağlı)
-- Vergi tipi enum değerlerini string'e çevir
UPDATE products SET tax_type = 'Standard' WHERE tax_type = '20';
UPDATE products SET tax_type = 'Reduced' WHERE tax_type = '10';
UPDATE products SET tax_type = 'Special' WHERE tax_type = '13';

-- Kategori alanını güncelle (örnek)
UPDATE products SET category = 'Hauptgerichte' WHERE category = 'main_dishes';
UPDATE products SET category = 'Getränke' WHERE category = 'beverages';
UPDATE products SET category = 'Desserts' WHERE category = 'desserts';

COMMIT;

-- 7. PERFORMANS İYİLEŞTİRMELERİ (Transaction dışında)
-- VACUUM ve ANALYZE - Transaction dışında çalıştırılmalı
VACUUM ANALYZE products;

-- =====================================================
-- KONTROL SORGULARI
-- =====================================================

-- Tablo yapısını kontrol et
SELECT column_name, data_type, is_nullable, column_default, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- Index'leri kontrol et
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products';

-- Constraint'leri kontrol et
SELECT conname, contype, pg_get_constraintdef(oid) 
FROM pg_constraint 
WHERE conrelid = 'products'::regclass;

-- Test verisini kontrol et
SELECT 
    name, 
    price, 
    tax_type, 
    category, 
    stock_quantity
FROM products 
LIMIT 5;

-- =====================================================
-- GERİ ALMA SCRIPTI (ROLLBACK)
-- =====================================================
/*
-- Eğer geri almak isterseniz:

BEGIN TRANSACTION;

-- Check constraint'leri kaldır
ALTER TABLE products DROP CONSTRAINT IF EXISTS CK_products_price_positive;
ALTER TABLE products DROP CONSTRAINT IF EXISTS CK_products_stock_quantity_non_negative;
ALTER TABLE products DROP CONSTRAINT IF EXISTS CK_products_min_stock_level_non_negative;

-- Index'leri kaldır
DROP INDEX IF EXISTS IX_products_barcode;
DROP INDEX IF EXISTS IX_products_category;
DROP INDEX IF EXISTS IX_products_tax_type;
DROP INDEX IF EXISTS UQ_products_barcode;

-- Alanları eski haline getir
ALTER TABLE products ALTER COLUMN tax_type TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN category TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN barcode TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN name TYPE VARCHAR(100);
ALTER TABLE products ALTER COLUMN description TYPE VARCHAR(500);

COMMIT;
*/
