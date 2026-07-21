using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class Wave3ATenantScopedCategoriesAndProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultTenant = LegacyDefaultTenantIds.Primary;

            // FixProductsCategoryIdUuidAndFk adds the FK without quoted identifiers, so PostgreSQL
            // stores it as fk_products_categories_category_id. EF DropForeignKey would quote
            // "FK_products_categories_category_id" and fail (42704). Drop by catalog match.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT c.conname AS name
        FROM pg_constraint c
        JOIN pg_class rel ON rel.oid = c.conrelid
        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
        WHERE nsp.nspname = 'public'
          AND rel.relname = 'products'
          AND c.contype = 'f'
          AND c.confrelid = 'categories'::regclass
          AND array_length(c.conkey, 1) = 1
          AND EXISTS (
              SELECT 1
              FROM pg_attribute a
              WHERE a.attrelid = c.conrelid
                AND a.attnum = c.conkey[1]
                AND a.attname = 'category_id')
    ) LOOP
        EXECUTE format('ALTER TABLE products DROP CONSTRAINT %I', r.name);
    END LOOP;
END $$;
");

            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_products_barcode"";
DROP INDEX IF EXISTS ix_products_barcode;
");

            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_categories_Name"";
DROP INDEX IF EXISTS ix_categories_name;
");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "categories",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_categories_id_tenant_id",
                table: "categories",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id_tenant_id",
                table: "products",
                columns: new[] { "category_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL AND barcode <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories",
                columns: new[] { "tenant_id", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_categories_tenants_tenant_id",
                table: "categories",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id_tenant_id",
                table: "products",
                columns: new[] { "category_id", "tenant_id" },
                principalTable: "categories",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tenants_tenant_id",
                table: "products",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categories_tenants_tenant_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_category_id_tenant_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tenants_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_categories_id_tenant_id",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
