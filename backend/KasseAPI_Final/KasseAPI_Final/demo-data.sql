-- Demo Kategoriler ve Ürünler Ekleme Scripti
-- RKSV uyumlu vergi grupları ile
-- Veritabanı kolon isimlerine göre düzeltildi

-- Önce mevcut verileri temizle (isteğe bağlı)
-- DELETE FROM "products" WHERE "category" IN ('Getränke', 'Speisen', 'Desserts', 'Snacks', 'Kaffee & Tee');
-- DELETE FROM "categories" WHERE "Name" IN ('Getränke', 'Speisen', 'Desserts', 'Snacks', 'Kaffee & Tee');

-- Kategoriler ekle (eğer yoksa) - Veritabanı kolon isimlerine göre
INSERT INTO "categories" ("id", "Name", "Description", "Color", "Icon", "SortOrder", "is_active", "created_at", "updated_at")
SELECT 
    gen_random_uuid(), 'Getränke', 'Alkoholfreie und alkoholische Getränke', '#3498db', 'wine', 1, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM "categories" WHERE "Name" = 'Getränke');

INSERT INTO "categories" ("id", "Name", "Description", "Color", "Icon", "SortOrder", "is_active", "created_at", "updated_at")
SELECT 
    gen_random_uuid(), 'Speisen', 'Hauptgerichte und Vorspeisen', '#e74c3c', 'restaurant', 2, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM "categories" WHERE "Name" = 'Speisen');

INSERT INTO "categories" ("id", "Name", "Description", "Color", "Icon", "SortOrder", "is_active", "created_at", "updated_at")
SELECT 
    gen_random_uuid(), 'Desserts', 'Süße Nachspeisen und Kuchen', '#f39c12', 'ice-cream', 3, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM "categories" WHERE "Name" = 'Desserts');

INSERT INTO "categories" ("id", "Name", "Description", "Color", "Icon", "SortOrder", "is_active", "created_at", "updated_at")
SELECT 
    gen_random_uuid(), 'Snacks', 'Kleine Zwischenmahlzeiten', '#27ae60', 'fast-food', 4, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM "categories" WHERE "Name" = 'Snacks');

INSERT INTO "categories" ("id", "Name", "Description", "Color", "Icon", "SortOrder", "is_active", "created_at", "updated_at")
SELECT 
    gen_random_uuid(), 'Kaffee & Tee', 'Heiße Getränke', '#8e44ad', 'cafe', 5, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM "categories" WHERE "Name" = 'Kaffee & Tee');

-- Getränke Kategorisi - Standard Tax (20%)
INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Cola 0.33L',
    'Erfrischendes Cola-Getränk',
    2.50,
    1, -- Standard Tax (20%)
    'Getränke',
    true,
    100,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Cola 0.33L' AND "category" = 'Getränke');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Bier 0.5L',
    'Helles Lagerbier',
    4.80,
    1, -- Standard Tax (20%)
    'Getränke',
    true,
    50,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Bier 0.5L' AND "category" = 'Getränke');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Wein 0.2L',
    'Rotwein aus Österreich',
    6.50,
    1, -- Standard Tax (20%)
    'Getränke',
    true,
    30,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Wein 0.2L' AND "category" = 'Getränke');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Mineralwasser 0.5L',
    'Natürliches Mineralwasser',
    1.80,
    1, -- Standard Tax (20%)
    'Getränke',
    true,
    80,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Mineralwasser 0.5L' AND "category" = 'Getränke');

-- Speisen Kategorisi - Standard Tax (20%)
INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Wiener Schnitzel',
    'Klassisches Wiener Schnitzel mit Pommes',
    18.90,
    1, -- Standard Tax (20%)
    'Speisen',
    true,
    25,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Wiener Schnitzel' AND "category" = 'Speisen');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Gulasch',
    'Traditionelles Rindergulasch mit Semmelknödel',
    16.50,
    1, -- Standard Tax (20%)
    'Speisen',
    true,
    20,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Gulasch' AND "category" = 'Speisen');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Salat',
    'Gemischter Salat mit hausgemachtem Dressing',
    8.90,
    1, -- Standard Tax (20%)
    'Speisen',
    true,
    15,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Salat' AND "category" = 'Speisen');

-- Desserts Kategorisi - Reduced Tax (10%)
INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Apfelstrudel',
    'Hausgemachter Apfelstrudel mit Vanillesauce',
    6.90,
    2, -- Reduced Tax (10%)
    'Desserts',
    true,
    12,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Apfelstrudel' AND "category" = 'Desserts');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Sachertorte',
    'Original Wiener Sachertorte',
    7.50,
    2, -- Reduced Tax (10%)
    'Desserts',
    true,
    10,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Sachertorte' AND "category" = 'Desserts');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Eisbecher',
    '3 Kugeln Eis mit Sahne und Schokoladensauce',
    5.90,
    2, -- Reduced Tax (10%)
    'Desserts',
    true,
    20,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Eisbecher' AND "category" = 'Desserts');

-- Snacks Kategorisi - Reduced Tax (10%)
INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Chips',
    'Kartoffelchips mit Salz',
    3.50,
    2, -- Reduced Tax (10%)
    'Snacks',
    true,
    40,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Chips' AND "category" = 'Snacks');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Nüsse',
    'Gemischte Nüsse 100g',
    4.20,
    2, -- Reduced Tax (10%)
    'Snacks',
    true,
    25,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Nüsse' AND "category" = 'Snacks');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Schokolade',
    'Milchschokolade 100g',
    2.80,
    2, -- Reduced Tax (10%)
    'Snacks',
    true,
    35,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Schokolade' AND "category" = 'Snacks');

-- Kaffee & Tee Kategorisi - Special Tax (13%)
INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Espresso',
    'Starker italienischer Espresso',
    3.20,
    3, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    60,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Espresso' AND "category" = 'Kaffee & Tee');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Cappuccino',
    'Cappuccino mit Milchschaum',
    4.50,
    3, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    45,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Cappuccino' AND "category" = 'Kaffee & Tee');

INSERT INTO "products" ("id", "name", "description", "price", "tax_type", "category", "is_active", "stock_quantity", "created_at", "updated_at", "barcode")
SELECT 
    gen_random_uuid(),
    'Tee',
    'Kräutertee aus Österreich',
    3.80,
    3, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    30,
    NOW(),
    NOW(),
    'BC-' || SUBSTR(gen_random_uuid()::text, 1, 8)
WHERE NOT EXISTS (SELECT 1 FROM "products" WHERE "name" = 'Tee' AND "category" = 'Kaffee & Tee');

-- Demo veriler eklendi mesajı
SELECT 
    'Demo veriler başarıyla eklendi!' as message,
    COUNT(DISTINCT c."id") as category_count,
    COUNT(p."id") as product_count,
    COUNT(CASE WHEN p."tax_type" = 1 THEN 1 END) as standard_tax_products,
    COUNT(CASE WHEN p."tax_type" = 2 THEN 1 END) as reduced_tax_products,
    COUNT(CASE WHEN p."tax_type" = 3 THEN 1 END) as special_tax_products
FROM "categories" c
LEFT JOIN "products" p ON c."Name" = p."category"
WHERE c."is_active" = true;
