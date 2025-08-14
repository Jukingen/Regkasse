-- Demo Kategoriler ve Ürünler Ekleme Scripti
-- RKSV uyumlu vergi grupları ile

-- Kategoriler ekle
INSERT INTO "Categories" ("Id", "Name", "Description", "Color", "Icon", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
VALUES 
    (gen_random_uuid(), 'Getränke', 'Alkoholfreie und alkoholische Getränke', '#3498db', 'wine', 1, true, NOW(), NOW()),
    (gen_random_uuid(), 'Speisen', 'Hauptgerichte und Vorspeisen', '#e74c3c', 'restaurant', 2, true, NOW(), NOW()),
    (gen_random_uuid(), 'Desserts', 'Süße Nachspeisen und Kuchen', '#f39c12', 'ice-cream', 3, true, NOW(), NOW()),
    (gen_random_uuid(), 'Snacks', 'Kleine Zwischenmahlzeiten', '#27ae60', 'fast-food', 4, true, NOW(), NOW()),
    (gen_random_uuid(), 'Kaffee & Tee', 'Heiße Getränke', '#8e44ad', 'cafe', 5, true, NOW(), NOW());

-- Getränke Kategorisi - Standard Tax (20%)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Cola 0.33L',
    'Erfrischendes Cola-Getränk',
    2.50,
    0, -- Standard Tax (20%)
    'Getränke',
    true,
    100,
    '4001234567890',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Getränke');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Bier 0.5L',
    'Helles Lagerbier',
    4.80,
    0, -- Standard Tax (20%)
    'Getränke',
    true,
    50,
    '4001234567891',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Getränke');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Wein 0.2L',
    'Rotwein aus Österreich',
    6.50,
    0, -- Standard Tax (20%)
    'Getränke',
    true,
    30,
    '4001234567892',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Getränke');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Mineralwasser 0.5L',
    'Natürliches Mineralwasser',
    1.80,
    0, -- Standard Tax (20%)
    'Getränke',
    true,
    80,
    '4001234567893',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Getränke');

-- Speisen Kategorisi - Standard Tax (20%)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Wiener Schnitzel',
    'Klassisches Wiener Schnitzel mit Pommes',
    18.90,
    0, -- Standard Tax (20%)
    'Speisen',
    true,
    25,
    '4001234567894',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Speisen');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Gulasch',
    'Traditionelles Rindergulasch mit Semmelknödel',
    16.50,
    0, -- Standard Tax (20%)
    'Speisen',
    true,
    20,
    '4001234567895',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Speisen');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Salat',
    'Gemischter Salat mit hausgemachtem Dressing',
    8.90,
    0, -- Standard Tax (20%)
    'Speisen',
    true,
    15,
    '4001234567896',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Speisen');

-- Desserts Kategorisi - Reduced Tax (10%)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Apfelstrudel',
    'Hausgemachter Apfelstrudel mit Vanillesauce',
    6.90,
    1, -- Reduced Tax (10%)
    'Desserts',
    true,
    12,
    '4001234567897',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Desserts');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Sachertorte',
    'Original Wiener Sachertorte',
    7.50,
    1, -- Reduced Tax (10%)
    'Desserts',
    true,
    10,
    '4001234567898',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Desserts');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Eisbecher',
    '3 Kugeln Eis mit Sahne und Schokoladensauce',
    5.90,
    1, -- Reduced Tax (10%)
    'Desserts',
    true,
    20,
    '4001234567899',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Desserts');

-- Snacks Kategorisi - Reduced Tax (10%)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Chips',
    'Kartoffelchips mit Salz',
    3.50,
    1, -- Reduced Tax (10%)
    'Snacks',
    true,
    40,
    '4001234567900',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Snacks');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Nüsse',
    'Gemischte Nüsse 100g',
    4.20,
    1, -- Reduced Tax (10%)
    'Snacks',
    true,
    25,
    '4001234567901',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Snacks');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Schokolade',
    'Milchschokolade 100g',
    2.80,
    1, -- Reduced Tax (10%)
    'Snacks',
    true,
    35,
    '4001234567902',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Snacks');

-- Kaffee & Tee Kategorisi - Special Tax (13%)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Espresso',
    'Starker italienischer Espresso',
    3.20,
    2, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    60,
    '4001234567903',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Kaffee & Tee');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Cappuccino',
    'Cappuccino mit Milchschaum',
    4.50,
    2, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    45,
    '4001234567904',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Kaffee & Tee');

INSERT INTO "Products" ("Id", "Name", "Description", "Price", "TaxType", "Category", "IsActive", "StockQuantity", "Barcode", "CreatedAt", "UpdatedAt")
SELECT 
    gen_random_uuid(),
    'Tee',
    'Kräutertee aus Österreich',
    3.80,
    2, -- Special Tax (13%)
    'Kaffee & Tee',
    true,
    30,
    '4001234567905',
    NOW(),
    NOW()
WHERE EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = 'Kaffee & Tee');

-- Demo veriler eklendi mesajı
SELECT 
    'Demo veriler başarıyla eklendi!' as message,
    COUNT(DISTINCT c."Id") as category_count,
    COUNT(p."Id") as product_count,
    COUNT(CASE WHEN p."TaxType" = 0 THEN 1 END) as standard_tax_products,
    COUNT(CASE WHEN p."TaxType" = 1 THEN 1 END) as reduced_tax_products,
    COUNT(CASE WHEN p."TaxType" = 2 THEN 1 END) as special_tax_products
FROM "Categories" c
LEFT JOIN "Products" p ON c."Name" = p."Category"
WHERE c."IsActive" = true;
