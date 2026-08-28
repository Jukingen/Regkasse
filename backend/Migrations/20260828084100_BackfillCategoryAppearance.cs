using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Data-only: backfill demo category colors and convert legacy Ionicons glyph names to emoji.
    /// Schema already has Icon, Color, and SortOrder (not display_order).
    /// </summary>
    public partial class BackfillCategoryAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE categories AS c
                SET "Color" = v.color
                FROM (VALUES
                    ('salate', '#7CB342'),
                    ('stangerl', '#FFB74D'),
                    ('baguettes', '#D4A574'),
                    ('calzone', '#8D6E63'),
                    ('pizza-mittel', '#E53935'),
                    ('pizza-partner', '#C62828'),
                    ('familien-pizza', '#B71C1C'),
                    ('mexikanische-pizza-mittel', '#FF6F00'),
                    ('mexikanische-pizza-partner', '#E65100'),
                    ('pasta', '#F9A825'),
                    ('imbiss', '#FF8F00'),
                    ('burger', '#6D4C41'),
                    ('kebap', '#8BC34A'),
                    ('desserts', '#EC407A'),
                    ('saucen', '#FF7043'),
                    ('alkoholfreie-getranke', '#29B6F6'),
                    ('getranke', '#3498DB'),
                    ('speisen', '#E74C3C'),
                    ('snacks', '#27AE60'),
                    ('kaffee-tee', '#8E44AD')
                ) AS v(category_key, color)
                WHERE c.category_key = v.category_key
                  AND (c."Color" IS NULL OR btrim(c."Color") = '');

                UPDATE categories
                SET "Icon" = CASE btrim("Icon")
                    WHEN 'wine' THEN '🍷'
                    WHEN 'restaurant' THEN '🍽️'
                    WHEN 'ice-cream' THEN '🍰'
                    WHEN 'fast-food' THEN '🍔'
                    WHEN 'cafe' THEN '☕'
                    WHEN 'folder' THEN '📦'
                    WHEN 'nutrition' THEN '🥗'
                    WHEN 'pizza' THEN '🍕'
                    ELSE "Icon"
                END
                WHERE btrim("Icon") IN (
                    'wine', 'restaurant', 'ice-cream', 'fast-food', 'cafe', 'folder', 'nutrition', 'pizza'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Appearance backfill is not reversed: existing tenant customizations must stay.
        }
    }
}
