-- =============================================================================
-- Table Orders Recovery - DB Validation Script
-- =============================================================================
-- Run this script when GET /api/cart/table-orders-recovery returns 503 with
-- errorCode: TABLE_ORDERS_MISSING (relation does not exist).
--
-- Usage (psql or pgAdmin):
--   \i scripts/db-validate-table-orders.sql
--   -- or copy-paste the queries below
-- =============================================================================

-- 1) Check if required tables exist in public schema
SELECT tablename, schemaname
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('table_orders', 'table_order_items', 'carts', 'cart_items', 'customers', 'AspNetUsers')
ORDER BY tablename;

-- Expected: 6 rows (table_orders, table_order_items, carts, cart_items, customers, AspNetUsers)
-- If any row is missing, the table does not exist -> run migrations.

-- 2) Check migration history (if __EFMigrationsHistory exists)
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%TableOrders%' OR "MigrationId" LIKE '%FixTableOrdersSync%'
ORDER BY "MigrationId" DESC
LIMIT 5;

-- Expected: 20260222222921_FixTableOrdersSync should appear if applied.

-- 3) Quick structure check for table_orders
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'table_orders'
ORDER BY ordinal_position;

-- =============================================================================
-- Fix: If tables are missing, run from backend folder:
--   dotnet ef database update
--
-- Verify migrations are applied:
--   dotnet ef migrations list
-- =============================================================================
