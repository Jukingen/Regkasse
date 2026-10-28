using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Data migration: resolve duplicate <c>(tenant_id, "Name")</c> and <c>(tenant_id, category_key)</c>
    /// rows in <c>categories</c> before/alongside unique indexes. Keeps the oldest row per group;
    /// deactivates and renames duplicates so inserts no longer hit <c>IX_categories_tenant_id_Name</c>.
    /// Irreversible — renamed rows cannot be inferred in Down.
    /// </summary>
    public partial class DeduplicateCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Duplicate display names within a tenant (matches IX_categories_tenant_id_Name).
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        tenant_id,
                        "Name",
                        category_key,
                        ROW_NUMBER() OVER (
                            PARTITION BY tenant_id, "Name"
                            ORDER BY created_at ASC NULLS LAST, id ASC
                        ) AS rn
                    FROM categories
                )
                UPDATE categories AS c
                SET
                    is_active = false,
                    "Name" = LEFT(
                        r."Name" || '_duplicate_' || SUBSTRING(REPLACE(r.id::text, '-', '') FROM 1 FOR 8),
                        100),
                    category_key = LEFT(
                        r.category_key || '_duplicate_' || SUBSTRING(REPLACE(r.id::text, '-', '') FROM 1 FOR 8),
                        100),
                    updated_at = NOW(),
                    updated_by = 'migration:DeduplicateCategories'
                FROM ranked AS r
                WHERE c.id = r.id
                  AND r.rn > 1;
                """);

            // 2) Duplicate category keys within a tenant (matches IX_categories_tenant_id_category_key).
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        category_key,
                        ROW_NUMBER() OVER (
                            PARTITION BY tenant_id, category_key
                            ORDER BY created_at ASC NULLS LAST, id ASC
                        ) AS rn
                    FROM categories
                )
                UPDATE categories AS c
                SET
                    is_active = false,
                    category_key = LEFT(
                        r.category_key || '_duplicate_' || SUBSTRING(REPLACE(r.id::text, '-', '') FROM 1 FOR 8),
                        100),
                    updated_at = NOW(),
                    updated_by = 'migration:DeduplicateCategories'
                FROM ranked AS r
                WHERE c.id = r.id
                  AND r.rn > 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; original names/keys before rename are not stored.
        }
    }
}
