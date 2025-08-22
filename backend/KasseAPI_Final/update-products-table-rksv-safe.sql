-- =====================================================
-- PRODUCT TABLOSU RKSV UYUMLU GÜNCELLEME SCRIPTI - GÜVENLİ VERSİYON
-- Her adım ayrı ayrı çalıştırılmalı
-- =====================================================

-- ADIM 1: Mevcut durumu kontrol et
SELECT 'Checking current table structure...' as status;

SELECT column_name, data_type, is_nullable, column_default, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- ADIM 2: Mevcut constraint'leri kontrol et
SELECT 'Checking current constraints...' as status;

SELECT conname, contype, pg_get_constraintdef(oid) 
FROM pg_constraint 
WHERE conrelid = 'products'::regclass;

-- ADIM 3: Mevcut index'leri kontrol et
SELECT 'Checking current indexes...' as status;

SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products';

-- ADIM 4: Mevcut veri örneklerini kontrol et
SELECT 'Checking sample data...' as status;

SELECT 
    name, 
    price, 
    tax_type, 
    category, 
    stock_quantity,
    min_stock_level
FROM products 
LIMIT 5;

-- ADIM 5: Alan uzunluklarını güncelle (tek tek)
SELECT 'Updating column lengths...' as status;

-- tax_type alanını güncelle
ALTER TABLE products ALTER COLUMN tax_type TYPE VARCHAR(20);

-- category alanını güncelle
ALTER TABLE products ALTER COLUMN category TYPE VARCHAR(100);

-- barcode alanını güncelle
ALTER TABLE products ALTER COLUMN barcode TYPE VARCHAR(100);

-- name alanını güncelle
ALTER TABLE products ALTER COLUMN name TYPE VARCHAR(200);

-- description alanını güncelle
ALTER TABLE products ALTER COLUMN description TYPE TEXT;

-- ADIM 6: Index'leri ekle (tek tek)
SELECT 'Creating indexes...' as status;

-- Barcode index'i
CREATE INDEX IF NOT EXISTS IX_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;

-- Category index'i
CREATE INDEX IF NOT EXISTS IX_products_category ON products(category);

-- Tax type index'i
CREATE INDEX IF NOT EXISTS IX_products_tax_type ON products(tax_type);

-- Unique barcode index'i
CREATE UNIQUE INDEX IF NOT EXISTS UQ_products_barcode ON products(barcode) WHERE barcode IS NOT NULL;

-- ADIM 7: Check constraint'leri ekle (tek tek)
SELECT 'Adding check constraints...' as status;

-- Fiyat pozitif olmalı
ALTER TABLE products ADD CONSTRAINT CK_products_price_positive CHECK (price >= 0);

-- Stok miktarı negatif olamaz
ALTER TABLE products ADD CONSTRAINT CK_products_stock_quantity_non_negative CHECK (stock_quantity >= 0);

-- Minimum stok seviyesi negatif olamaz
ALTER TABLE products ADD CONSTRAINT CK_products_min_stock_level_non_negative CHECK (min_stock_level >= 0);

-- ADIM 8: Yorumları ekle
SELECT 'Adding comments...' as status;

COMMENT ON TABLE products IS 'RKSV uyumlu ürün tablosu - Avusturya kasa sistemi standartlarına uygun';
COMMENT ON COLUMN products.tax_type IS 'RKSV vergi tipi: Standard(20%), Reduced(10%), Special(13%)';
COMMENT ON COLUMN products.category IS 'Ürün kategorisi';
COMMENT ON COLUMN products.barcode IS 'Barkod (EAN-13, UPC, vb.)';
COMMENT ON COLUMN products.name IS 'Ürün adı';
COMMENT ON COLUMN products.description IS 'Ürün açıklaması';

-- ADIM 9: Veri güncellemeleri (isteğe bağlı)
SELECT 'Updating data...' as status;

-- Vergi tipi enum değerlerini string'e çevir
UPDATE products SET tax_type = 'Standard' WHERE tax_type = '20';
UPDATE products SET tax_type = 'Reduced' WHERE tax_type = '10';
UPDATE products SET tax_type = 'Special' WHERE tax_type = '13';

-- Kategori alanını güncelle (örnek)
UPDATE products SET category = 'Hauptgerichte' WHERE category = 'main_dishes';
UPDATE products SET category = 'Getränke' WHERE category = 'beverages';
UPDATE products SET category = 'Desserts' WHERE category = 'desserts';

-- ADIM 10: Final kontrol
SELECT 'Final check...' as status;

-- Güncellenmiş tablo yapısını kontrol et
SELECT column_name, data_type, is_nullable, column_default, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;

-- Yeni index'leri kontrol et
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products';

-- Yeni constraint'leri kontrol et
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

SELECT 'Update completed successfully!' as status;
