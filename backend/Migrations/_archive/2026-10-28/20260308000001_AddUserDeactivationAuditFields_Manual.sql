-- Manual script: add deactivation audit columns to AspNetUsers if missing.
-- Run this on your PostgreSQL database if migration was not applied (e.g. "column deactivated_at does not exist").
-- Safe to run: each block uses "IF NOT EXISTS" / "DO $$ ... END $$" to avoid errors if columns already exist.

-- Add deactivated_at (timestamp with time zone, nullable)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'aspnetusers' AND column_name = 'deactivated_at'
  ) THEN
    ALTER TABLE "AspNetUsers" ADD COLUMN deactivated_at timestamp with time zone NULL;
  END IF;
END $$;

-- Add deactivated_by (varchar 450, nullable)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'aspnetusers' AND column_name = 'deactivated_by'
  ) THEN
    ALTER TABLE "AspNetUsers" ADD COLUMN deactivated_by character varying(450) NULL;
  END IF;
END $$;

-- Add deactivation_reason (varchar 500, nullable)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'aspnetusers' AND column_name = 'deactivation_reason'
  ) THEN
    ALTER TABLE "AspNetUsers" ADD COLUMN deactivation_reason character varying(500) NULL;
  END IF;
END $$;

-- Optional: register in EF migrations history so "dotnet ef database update" won't re-apply this migration
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260308000001_AddUserDeactivationAuditFields', '10.0.0'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260308000001_AddUserDeactivationAuditFields');
