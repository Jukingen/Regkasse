CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;
INSERT INTO public.products (
    id,
    name, 
    description,
    price, 
    tax_type, 
    category, 
    stock_quantity, 
    min_stock_level, 
    unit, 
    cost, 
    tax_rate, 
    is_fiscal_compliant, 
    is_taxable, 
    rksv_product_type, 
    created_at, 
    updated_at, 
    is_active, 
    category_id,
    barcode
)
SELECT 
    gen_random_uuid(),
    'Demo Product ' || i,
    'Description for product ' || i,
    ROUND((RANDOM() * 100 + 10)::numeric, 2),
    1,
    'Demo Category',
    (RANDOM() * 50 + 10)::int,
    5,
    'Piece',
    ROUND((RANDOM() * 50 + 5)::numeric, 2),
    20.00,
    true,
    true,
    'Standard',
    now(),
    now(),
    true,
    NULL,
    'BARCODE-' || lpad(i::text, 6, '0')
FROM generate_series(1, 10) as i;
COMMIT;
