-- Veritabanı durumunu kontrol et
-- Kategoriler
SELECT 
    'Categories' as table_name,
    COUNT(*) as total_count,
    STRING_AGG("Name", ', ') as category_names
FROM "Categories" 
WHERE "IsActive" = true;

-- Ürünler
SELECT 
    'Products' as table_name,
    COUNT(*) as total_count,
    COUNT(DISTINCT "Category") as unique_categories,
    STRING_AGG(DISTINCT "Category", ', ') as product_categories
FROM "Products" 
WHERE "IsActive" = true;

-- Kategori-Ürün ilişkisi
SELECT 
    c."Name" as category_name,
    COUNT(p."Id") as product_count,
    STRING_AGG(p."Name", ', ') as products
FROM "Categories" c
LEFT JOIN "Products" p ON c."Name" = p."Category"
WHERE c."IsActive" = true
GROUP BY c."Id", c."Name"
ORDER BY c."SortOrder";

-- Ürün detayları
SELECT 
    p."Name" as product_name,
    p."Category" as category,
    p."Price",
    p."TaxType",
    p."StockQuantity",
    p."IsActive"
FROM "Products" p
WHERE p."IsActive" = true
ORDER BY p."Category", p."Name";
