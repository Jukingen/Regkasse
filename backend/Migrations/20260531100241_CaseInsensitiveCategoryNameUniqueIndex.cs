using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Replaces case-sensitive <c>IX_categories_tenant_id_Name</c> with a functional unique index on
    /// <c>lower(trim("Name"))</c> per tenant. Uses <c>lower()</c> instead of <c>citext</c> to avoid
    /// requiring the extension on managed PostgreSQL hosts; aligns with API duplicate checks.
    /// Run after <see cref="DeduplicateCategories"/>.
    /// </summary>
    public partial class CaseInsensitiveCategoryNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Resolve case-insensitive name collisions (e.g. "Salate" + "salate") before new index.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        tenant_id,
                        "Name",
                        category_key,
                        ROW_NUMBER() OVER (
                            PARTITION BY tenant_id, lower(trim("Name"))
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
                    updated_by = 'migration:CaseInsensitiveCategoryNameUniqueIndex'
                FROM ranked AS r
                WHERE c.id = r.id
                  AND r.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_categories_tenant_id_Name_ci"
                ON categories (tenant_id, lower(trim("Name")));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_categories_tenant_id_Name_ci";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories",
                columns: new[] { "tenant_id", "Name" },
                unique: true);
        }
    }
}
