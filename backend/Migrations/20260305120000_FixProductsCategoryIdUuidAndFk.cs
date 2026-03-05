using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Production-safe: products.category_id text → uuid, deterministic Uncategorized,
    /// invalid category_id'ler tek UPDATE ile düzeltilir, FK/index idempotent.
    /// </summary>
    public partial class FixProductsCategoryIdUuidAndFk : Migration
    {
        /// <summary>Deterministik "Uncategorized" kategori id'si (tüm ortamlarda aynı).</summary>
        private const string UncategoryIdDeterministic = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 1) Uncategorized kategorisi deterministik ----
            // A) Çoklu "Uncategorized" varsa fail-fast; B) yoksa INSERT; C) id sabit UUID
            migrationBuilder.Sql($@"
                DO $$
                DECLARE
                    uncat_count int;
                    uncat_id uuid;
                BEGIN
                    SELECT COUNT(*) INTO uncat_count
                    FROM categories
                    WHERE LOWER(TRIM(""Name"")) = 'uncategorized';

                    IF uncat_count > 1 THEN
                        RAISE EXCEPTION 'Migration: categories tablosunda Uncategorized adiyla % kayit var. Once tek kayda indirin. Cleanup icin backend/docs/DB_SCHEMA_PRODUCTS_CATEGORIES_TYPES.md dosyasina bakin.', uncat_count;
                    END IF;

                    IF uncat_count = 0 THEN
                        INSERT INTO categories (id, ""Name"", ""Description"", ""Color"", ""Icon"", ""SortOrder"", vat_rate, created_at, updated_at, is_active, created_by, updated_by)
                        VALUES ('{UncategoryIdDeterministic}'::uuid, 'Uncategorized', NULL, NULL, NULL, 999, 20, NOW(), NOW(), true, 'migration', 'migration');
                        uncat_id := '{UncategoryIdDeterministic}'::uuid;
                    ELSE
                        SELECT id INTO uncat_id FROM categories
                        WHERE LOWER(TRIM(""Name"")) = 'uncategorized'
                        LIMIT 1;
                    END IF;

                    -- 2) Geçersiz category_id'leri (NULL, boş, regex dışı) tek seferde Uncategorized yap
                    UPDATE products
                    SET category_id = uncat_id::text
                    WHERE category_id IS NULL
                       OR TRIM(category_id) = ''
                       OR category_id !~ '^[0-9a-fA-F]{{8}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{12}}$';
                END $$;
            ");

            // ---- 3) Kolon tipi text → uuid (artık tüm değerler geçerli UUID) ----
            migrationBuilder.Sql(@"
                ALTER TABLE products
                ALTER COLUMN category_id TYPE uuid USING category_id::uuid;
            ");

            // ---- 4) Index: yoksa oluştur ----
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_products_category_id"" ON products (category_id);
            ");

            // ---- 5) FK: yoksa ekle (varsa dokunma) ----
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE table_schema = 'public' AND table_name = 'products' AND constraint_name = 'FK_products_categories_category_id'
                    ) THEN
                        ALTER TABLE products
                        ADD CONSTRAINT FK_products_categories_category_id
                        FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // FK kaldır (varsa)
            migrationBuilder.Sql(@"
                ALTER TABLE products DROP CONSTRAINT IF EXISTS FK_products_categories_category_id;
            ");

            // category_id uuid → text
            migrationBuilder.Sql(@"
                ALTER TABLE products
                ALTER COLUMN category_id TYPE text USING category_id::text;
            ");

            // Index: Down'da eski haline dönüyoruz; index önceden de vardı (category_id üzerinde), bırakıyoruz.
            // İstenirse: DROP INDEX IF EXISTS ""IX_products_category_id"";
        }
    }
}
