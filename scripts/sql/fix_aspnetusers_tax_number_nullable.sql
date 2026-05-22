-- Idempotent fix: AspNetUsers.tax_number optional (RKSV company UID lives in company_settings).
-- Safe to run before/after EF migration 20260521224344_MakeAspNetUsersTaxNumberNullable.

DROP INDEX IF EXISTS "IX_AspNetUsers_tax_number";

DO $EF$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'AspNetUsers'
          AND column_name = 'tax_number'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE "AspNetUsers" ALTER COLUMN tax_number DROP NOT NULL;
    END IF;
END $EF$;

UPDATE "AspNetUsers"
SET tax_number = NULL
WHERE tax_number IS NOT NULL AND btrim(tax_number) = '';

WITH ranked AS (
    SELECT "Id",
           ROW_NUMBER() OVER (PARTITION BY tax_number ORDER BY "Id") AS rn
    FROM "AspNetUsers"
    WHERE tax_number IS NOT NULL AND btrim(tax_number) <> ''
)
UPDATE "AspNetUsers" AS u
SET tax_number = NULL
FROM ranked AS r
WHERE u."Id" = r."Id" AND r.rn > 1;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AspNetUsers_tax_number"
    ON "AspNetUsers" (tax_number)
    WHERE (tax_number IS NOT NULL AND tax_number <> '');

-- Verification
SELECT "Email", tax_number
FROM "AspNetUsers"
WHERE tax_number IS NOT NULL
ORDER BY tax_number, "Email";
