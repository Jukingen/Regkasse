using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Data migration: resolve case-insensitive duplicate <see cref="AspNetUsers.UserName"/> values
    /// and backfill <see cref="AspNetUsers.NormalizedUserName"/> (Identity upper-invariant).
    /// Irreversible — renamed duplicate rows cannot be inferred in Down.
    /// </summary>
    public partial class NormalizeUsernamesCaseInsensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Within each UPPER(UserName) group, keep the oldest user (created_at, then Id); suffix others.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        "Id",
                        "UserName",
                        ROW_NUMBER() OVER (
                            PARTITION BY UPPER(TRIM("UserName"))
                            ORDER BY created_at ASC NULLS LAST, "Id" ASC
                        ) AS rn
                    FROM "AspNetUsers"
                    WHERE "UserName" IS NOT NULL
                      AND TRIM("UserName") <> ''
                )
                UPDATE "AspNetUsers" AS u
                SET
                    "UserName" = LEFT(
                        r."UserName" || '_dup' || SUBSTRING(REPLACE(r."Id", '-', '') FROM 1 FOR 8),
                        256),
                    "NormalizedUserName" = LEFT(
                        UPPER(r."UserName" || '_dup' || SUBSTRING(REPLACE(r."Id", '-', '') FROM 1 FOR 8)),
                        256)
                FROM ranked AS r
                WHERE u."Id" = r."Id"
                  AND r.rn > 1;
                """);

            // 2) Backfill NormalizedUserName for all rows (and fix any remaining drift).
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "NormalizedUserName" = UPPER(TRIM("UserName"))
                WHERE "UserName" IS NOT NULL
                  AND TRIM("UserName") <> ''
                  AND (
                      "NormalizedUserName" IS NULL
                      OR "NormalizedUserName" IS DISTINCT FROM UPPER(TRIM("UserName"))
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; original usernames before rename are not stored.
        }
    }
}
