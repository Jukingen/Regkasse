-- Check if deactivation columns exist on AspNetUsers. Run in psql or any PostgreSQL client.
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'aspnetusers'
  AND column_name IN ('deactivated_at', 'deactivated_by', 'deactivation_reason')
ORDER BY column_name;
