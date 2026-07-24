using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Backfills <c>products.tax_group_id</c> from the tenant MwSt catalog (match by <c>tax_rate</c>),
/// ensuring system tax groups exist per tenant, then enforces NOT NULL.
/// Column/FK were introduced in <see cref="AddProductTaxGroupId"/>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725000000_AddTaxGroupsToProducts")]
public partial class AddTaxGroupsToProducts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Defensive: column may already exist from 20260724130000_AddProductTaxGroupId.
        migrationBuilder.Sql("""
            ALTER TABLE products
            ADD COLUMN IF NOT EXISTS tax_group_id uuid NULL;
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_products_tax_group_id"
            ON products (tax_group_id);
            """);

        // Seed Austrian system tax groups for every tenant that has products but is missing a code.
        migrationBuilder.Sql("""
            INSERT INTO tax_groups (
                id, tenant_id, name, description, rate,
                is_default, is_system, color, icon, group_type, austrian_code,
                valid_from, valid_to, replaced_by,
                created_at, updated_at, created_by, updated_by, is_active
            )
            SELECT
                gen_random_uuid(),
                t.id,
                v.name,
                v.description,
                v.rate,
                v.is_default,
                TRUE,
                v.color,
                v.icon,
                v.group_type,
                v.austrian_code,
                NULL,
                NULL,
                NULL,
                NOW() AT TIME ZONE 'utc',
                NOW() AT TIME ZONE 'utc',
                'tax-group-backfill',
                NULL,
                TRUE
            FROM tenants t
            CROSS JOIN (
                VALUES
                    ('Normalsatz', '20% MwSt. - Standard', 20.00, TRUE,  '#1890ff', '💰', 0, 'A'),
                    ('Ermäßigt', '10% MwSt. - Lebensmittel, Bücher', 10.00, FALSE, '#52c41a', '🛒', 1, 'B'),
                    ('Ermäßigt (Neu)', '4,9% MwSt. - E-Books, bestimmte Lebensmittel', 4.90, FALSE, '#faad14', '📚', 2, 'C'),
                    ('Mittelsteuersatz', '13% MwSt. - Tourismus, Dienstleistungen', 13.00, FALSE, '#722ed1', '🏨', 3, 'D'),
                    ('Nullsteuersatz', '0% MwSt. - Export', 0.00, FALSE, '#8c8c8c', '🌍', 4, 'E')
            ) AS v(name, description, rate, is_default, color, icon, group_type, austrian_code)
            WHERE EXISTS (SELECT 1 FROM products p WHERE p.tenant_id = t.id)
              AND NOT EXISTS (
                    SELECT 1
                    FROM tax_groups tg
                    WHERE tg.tenant_id = t.id
                      AND tg.austrian_code = v.austrian_code
              );
            """);

        // 1) Match by exact tax_rate (covers 0 / 4.9 / 10 / 13 / 20).
        migrationBuilder.Sql("""
            UPDATE products p
            SET tax_group_id = matched.id
            FROM (
                SELECT DISTINCT ON (tg.tenant_id, tg.rate)
                    tg.id,
                    tg.tenant_id,
                    tg.rate
                FROM tax_groups tg
                WHERE tg.is_active = TRUE
                ORDER BY tg.tenant_id, tg.rate, tg.is_system DESC, tg.is_default DESC, tg.created_at ASC
            ) AS matched
            WHERE p.tax_group_id IS NULL
              AND matched.tenant_id = p.tenant_id
              AND matched.rate = p.tax_rate;
            """);

        // 2) Fallback: tenant default group.
        migrationBuilder.Sql("""
            UPDATE products p
            SET tax_group_id = (
                SELECT tg.id
                FROM tax_groups tg
                WHERE tg.tenant_id = p.tenant_id
                  AND tg.is_default = TRUE
                  AND tg.is_active = TRUE
                ORDER BY tg.is_system DESC, tg.created_at ASC
                LIMIT 1
            )
            WHERE p.tax_group_id IS NULL;
            """);

        // 3) Fallback: Normalsatz 20%.
        migrationBuilder.Sql("""
            UPDATE products p
            SET tax_group_id = (
                SELECT tg.id
                FROM tax_groups tg
                WHERE tg.tenant_id = p.tenant_id
                  AND tg.rate = 20
                  AND tg.is_active = TRUE
                ORDER BY tg.is_system DESC, tg.is_default DESC, tg.created_at ASC
                LIMIT 1
            )
            WHERE p.tax_group_id IS NULL;
            """);

        // 4) Last resort: Ermäßigt 10% (legacy food catalog default).
        migrationBuilder.Sql("""
            UPDATE products p
            SET tax_group_id = (
                SELECT tg.id
                FROM tax_groups tg
                WHERE tg.tenant_id = p.tenant_id
                  AND tg.rate = 10
                  AND tg.is_active = TRUE
                ORDER BY tg.is_system DESC, tg.created_at ASC
                LIMIT 1
            )
            WHERE p.tax_group_id IS NULL;
            """);

        // Replace SetNull FK with Restrict before enforcing NOT NULL.
        migrationBuilder.Sql("""
            ALTER TABLE products
            DROP CONSTRAINT IF EXISTS "FK_products_tax_groups_tax_group_id";
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM products WHERE tax_group_id IS NULL) THEN
                    RAISE EXCEPTION 'AddTaxGroupsToProducts: products.tax_group_id still NULL after backfill';
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "tax_group_id",
            table: "products",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_products_tax_groups_tax_group_id",
            table: "products",
            column: "tax_group_id",
            principalTable: "tax_groups",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_products_tax_groups_tax_group_id",
            table: "products");

        migrationBuilder.AlterColumn<Guid>(
            name: "tax_group_id",
            table: "products",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: false);

        migrationBuilder.AddForeignKey(
            name: "FK_products_tax_groups_tax_group_id",
            table: "products",
            column: "tax_group_id",
            principalTable: "tax_groups",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }
}
