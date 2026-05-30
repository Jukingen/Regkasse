using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryKeyAndFiscalCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category_key",
                table: "categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_demo_name",
                table: "categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fiscal_category",
                table: "categories",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_system_category",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                WITH normalized AS (
                    SELECT
                        id,
                        tenant_id,
                        created_at,
                        LOWER(
                            TRIM(BOTH '-' FROM
                                REGEXP_REPLACE(
                                    REGEXP_REPLACE(
                                        TRIM("Name"),
                                        '[^a-zA-Z0-9]+',
                                        '-',
                                        'g'
                                    ),
                                    '-+',
                                    '-',
                                    'g'
                                )
                            )
                        ) AS base_key,
                        ROW_NUMBER() OVER (
                            PARTITION BY tenant_id,
                                LOWER(
                                    TRIM(BOTH '-' FROM
                                        REGEXP_REPLACE(
                                            REGEXP_REPLACE(
                                                TRIM("Name"),
                                                '[^a-zA-Z0-9]+',
                                                '-',
                                                'g'
                                            ),
                                            '-+',
                                            '-',
                                            'g'
                                        )
                                    )
                                )
                            ORDER BY created_at, id
                        ) AS rn
                    FROM categories
                )
                UPDATE categories c
                SET category_key = CASE
                    WHEN COALESCE(NULLIF(n.base_key, ''), '') = '' THEN 'category-' || SUBSTRING(c.id::text, 1, 8)
                    WHEN n.rn = 1 THEN n.base_key
                    ELSE n.base_key || '-' || n.rn::text
                END
                FROM normalized n
                WHERE c.id = n.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "category_key",
                table: "categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id_category_key",
                table: "categories",
                columns: new[] { "tenant_id", "category_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_tenant_id_category_key",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "category_key",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "original_demo_name",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "fiscal_category",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "is_system_category",
                table: "categories");
        }
    }
}
