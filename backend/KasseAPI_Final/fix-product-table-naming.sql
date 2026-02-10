-- =====================================================
-- PRODUCT TABLOSU NAMING CONVENTION DÜZELTME SCRIPTI
-- PostgreSQL standartlarına uygun hale getirme
-- =====================================================

-- Mevcut tabloyu yedekle (isteğe bağlı)
-- CREATE TABLE products_backup AS SELECT * FROM products;

BEGIN TRANSACTION;

-- 1. ALAN İSİMLERİNİ DÜZELT (PostgreSQL naming convention)
-- Cost -> cost (lowercase)
ALTER TABLE products RENAME COLUMN "Cost" TO cost;

-- TaxRate -> tax_rate (snake_case)
ALTER TABLE products RENAME COLUMN "TaxRate" TO tax_rate;

-- CategoryId -> category_id (snake_case)
ALTER TABLE products RENAME COLUMN "CategoryId" TO category_id;

-- 2. ALAN UZUNLUKLARINI GÜNCELLE (RKSV uyumluluğu için)
ALTER TABLE products ALTER COLUMN tax_type TYPE VARCHAR(20);
ALTER TABLE products ALTER COLUMN category TYPE VARCHAR(100);
ALTER TABLE products ALTER COLUMN barcode TYPE VARCHAR(100);
ALTER TABLE products ALTER COLUMN name TYPE VARCHAR(200);
ALTER TABLE products ALTER COLUMN description TYPE TEXT;

-- 3. YENİ INDEX'LER EKLE
CREATE INDEX IF NOT EXISTS IX_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_products_category ON products(category);
CREATE INDEX IF NOT EXISTS IX_products_tax_type ON products(tax_type);
CREATE INDEX IF NOT EXISTS IX_products_category_id ON products(category_id);

-- 4. UNIQUE CONSTRAINT EKLE
CREATE UNIQUE INDEX IF NOT EXISTS UQ_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;

-- 5. CHECK CONSTRAINT'LER EKLE
ALTER TABLE products ADD CONSTRAINT CK_products_price_positive CHECK (price >= 0);
ALTER TABLE products ADD CONSTRAINT CK_products_stock_quantity_non_negative CHECK (stock_quantity >= 0);
ALTER TABLE products ADD CONSTRAINT CK_products_min_stock_level_non_negative CHECK (min_stock_level >= 0);
ALTER TABLE products ADD CONSTRAINT CK_products_cost_non_negative CHECK (cost >= 0);
ALTER TABLE products ADD CONSTRAINT CK_products_tax_rate_range CHECK (tax_rate >= 0 AND tax_rate <= 100);

-- 6. YORUMLAR EKLE
COMMENT ON TABLE products IS 'RKSV uyumlu ürün tablosu - PostgreSQL naming convention standartlarına uygun';
COMMENT ON COLUMN products.tax_type IS 'RKSV vergi tipi: Standard(20%), Reduced(10%), Special(13%)';
COMMENT ON COLUMN products.category IS 'Ürün kategorisi';
COMMENT ON COLUMN products.category_id IS 'Kategori ID referansı';
COMMENT ON COLUMN products.barcode IS 'Barkod (EAN-13, UPC, vb.)';
COMMENT ON COLUMN products.name IS 'Ürün adı';
COMMENT ON COLUMN products.description IS 'Ürün açıklaması';
COMMENT ON COLUMN products.cost IS 'Ürün maliyeti';
COMMENT ON COLUMN products.tax_rate IS 'Vergi oranı (%)';

-- 7. VERİ GÜNCELLEMELERİ (isteğe bağlı)
-- Vergi tipi enum değerlerini string'e çevir
UPDATE products SET tax_type = 'Standard' WHERE tax_type = '20';
UPDATE products SET tax_type = 'Reduced' WHERE tax_type = '10';
UPDATE products SET tax_type = 'Special' WHERE tax_type = '13';

-- Kategori alanını güncelle (örnek)
UPDATE products SET category = 'Hauptgerichte' WHERE category = 'main_dishes';
UPDATE products SET category = 'Getränke' WHERE category = 'beverages';
UPDATE products SET category = 'Desserts' WHERE category = 'desserts';

-- 8. DEFAULT DEĞERLER EKLE
-- cost alanı için default değer
ALTER TABLE products ALTER COLUMN cost SET DEFAULT 0.00;

-- tax_rate alanı için default değer
ALTER TABLE products ALTER COLUMN tax_rate SET DEFAULT 20.00;

-- min_stock_level alanı için default değer
ALTER TABLE products ALTER COLUMN min_stock_level SET DEFAULT 0;

COMMIT;

-- =====================================================
-- KONTROL SORGULARI
-- =====================================================

-- Güncellenmiş tablo yapısını kontrol et
SELECT 'Updated table structure:' as info;
SELECT column_name, data_type, is_nullable, column_default, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- Yeni index'leri kontrol et
SELECT 'New indexes:' as info;
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products';

-- Yeni constraint'leri kontrol et
SELECT 'New constraints:' as info;
SELECT conname, contype, pg_get_constraintdef(oid) 
FROM pg_constraint 
WHERE conrelid = 'products'::regclass;

-- Test verisini kontrol et
SELECT 'Sample data:' as info;
SELECT 
    name, 
    price, 
    tax_type, 
    category, 
    stock_quantity,
    cost,
    tax_rate
FROM products 
LIMIT 5;

SELECT 'Update completed successfully!' as status;

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
ALTER TABLE products DROP CONSTRAINT IF EXISTS CK_products_cost_non_negative;
ALTER TABLE products DROP CONSTRAINT IF EXISTS CK_products_tax_rate_range;

-- Index'leri kaldır
DROP INDEX IF EXISTS IX_products_barcode;
DROP INDEX IF EXISTS IX_products_category;
DROP INDEX IF EXISTS IX_products_tax_type;
DROP INDEX IF EXISTS IX_products_category_id;
DROP INDEX IF EXISTS UQ_products_barcode;

-- Alan isimlerini eski haline getir
ALTER TABLE products RENAME COLUMN cost TO "Cost";
ALTER TABLE products RENAME COLUMN tax_rate TO "TaxRate";
ALTER TABLE products RENAME COLUMN category_id TO "CategoryId";

-- Alanları eski haline getir
ALTER TABLE products ALTER COLUMN tax_type TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN category TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN barcode TYPE VARCHAR(50);
ALTER TABLE products ALTER COLUMN name TYPE VARCHAR(100);
ALTER TABLE products ALTER COLUMN description TYPE VARCHAR(500);

COMMIT;
*/
