using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>Category VAT oranı; ürünlerin zorunlu kategori bağlantısı. VAT yüzde (10, 20). "Alle" DB'ye yazılmaz.</summary>
    public partial class AddCategoryVatAndRequiredCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "categories",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.Sql(@"
                UPDATE categories SET vat_rate = 10 WHERE LOWER(TRIM(""Name"")) = 'speisen';
                UPDATE categories SET vat_rate = 20 WHERE LOWER(TRIM(""Name"")) = 'getränke';
            ");

            migrationBuilder.Sql(@"
                INSERT INTO categories (id, ""Name"", ""Description"", ""Color"", ""Icon"", ""SortOrder"", vat_rate, created_at, updated_at, is_active, created_by, updated_by)
                SELECT gen_random_uuid(), d.cat_name, NULL, NULL, NULL, 0, 20, NOW(), NOW(), true, 'migration', 'migration'
                FROM (
                    SELECT DISTINCT TRIM(category) AS cat_name FROM products
                    WHERE category_id IS NULL AND TRIM(COALESCE(category,'')) <> ''
                ) d
                WHERE NOT EXISTS (SELECT 1 FROM categories c WHERE LOWER(TRIM(c.""Name"")) = LOWER(d.cat_name) AND c.is_active = true);
            ");

            migrationBuilder.Sql(@"
                UPDATE products p SET category_id = c.id
                FROM categories c
                WHERE p.category_id IS NULL AND LOWER(TRIM(COALESCE(p.category,''))) = LOWER(TRIM(c.""Name"")) AND c.is_active = true;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO categories (id, ""Name"", ""Description"", ""Color"", ""Icon"", ""SortOrder"", vat_rate, created_at, updated_at, is_active, created_by, updated_by)
                SELECT gen_random_uuid(), 'Uncategorized', NULL, NULL, NULL, 999, 20, NOW(), NOW(), true, 'migration', 'migration'
                WHERE NOT EXISTS (SELECT 1 FROM categories WHERE LOWER(TRIM(""Name"")) = 'uncategorized' AND is_active = true);
            ");

            migrationBuilder.Sql(@"
                UPDATE products p SET category_id = (SELECT id FROM categories WHERE LOWER(TRIM(""Name"")) = 'uncategorized' AND is_active = true LIMIT 1)
                WHERE p.category_id IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "products",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: false);

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "categories");
        }
    }
}
