using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// AspNetUsers.tax_number: optional profile field (not RKSV company UID).
    /// Clears empty strings, dedupes accidental duplicates, nullable column, partial unique index.
    /// </summary>
    public partial class MakeAspNetUsersTaxNumberNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_AspNetUsers_tax_number";

                UPDATE "AspNetUsers"
                SET tax_number = ''
                WHERE tax_number IS NULL;

                DO $EF$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'tax_number'
                          AND is_nullable = 'YES'
                    ) THEN
                        ALTER TABLE "AspNetUsers"
                            ALTER COLUMN tax_number SET NOT NULL;
                    END IF;
                END $EF$;

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AspNetUsers_tax_number"
                    ON "AspNetUsers" (tax_number);
                """);
        }
    }
}
