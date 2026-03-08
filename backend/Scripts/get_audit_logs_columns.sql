-- Run against your PostgreSQL database to get actual audit_logs schema (schema-first analysis).
-- Example: psql -U postgres -d YourDb -f get_audit_logs_columns.sql

-- 1) Table name(s) that might be audit log
SELECT 'Tables matching audit' AS step;
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND table_name ILIKE '%audit%'
ORDER BY table_schema, table_name;

-- 2) Columns of audit_logs (replace table name if yours is different, e.g. "AuditLogs")
SELECT 'Columns of audit_logs' AS step;
SELECT ordinal_position,
       column_name,
       data_type,
       character_maximum_length,
       is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'audit_logs'
ORDER BY ordinal_position;

-- 3) If your table is AuditLogs (PascalCase) instead of audit_logs, uncomment and run:
-- SELECT ordinal_position, column_name, data_type, character_maximum_length, is_nullable
-- FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'AuditLogs'
-- ORDER BY ordinal_position;
