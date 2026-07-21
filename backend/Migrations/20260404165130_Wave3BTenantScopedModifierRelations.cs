using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class Wave3BTenantScopedModifierRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultTenant = LegacyDefaultTenantIds.Primary;
            var defaultTenantSql = defaultTenant.ToString();

            migrationBuilder.DropForeignKey(
                name: "FK_addon_group_products_product_modifier_groups_modifier_group~",
                table: "addon_group_products");

            migrationBuilder.DropForeignKey(
                name: "FK_addon_group_products_products_product_id",
                table: "addon_group_products");

            migrationBuilder.DropForeignKey(
                name: "FK_product_modifier_group_assignments_product_modifier_groups_~",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_product_modifier_group_assignments_products_product_id",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropIndex(
                name: "IX_product_modifier_group_assignments_modifier_group_id",
                table: "product_modifier_group_assignments");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "product_modifier_groups",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_groups_tenant_id",
                table: "product_modifier_groups",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_product_modifier_groups_tenants_tenant_id",
                table: "product_modifier_groups",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_products_id_tenant_id",
                table: "products",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "product_modifier_group_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($@"
                UPDATE product_modifier_group_assignments AS a
                SET tenant_id = p.tenant_id
                FROM products AS p
                WHERE a.product_id = p.id;
                UPDATE product_modifier_group_assignments
                SET tenant_id = '{defaultTenantSql}'::uuid
                WHERE tenant_id IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "product_modifier_group_assignments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "addon_group_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($@"
                UPDATE addon_group_products AS ag
                SET tenant_id = p.tenant_id
                FROM products AS p
                WHERE ag.product_id = p.id;
                UPDATE addon_group_products
                SET tenant_id = '{defaultTenantSql}'::uuid
                WHERE tenant_id IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "addon_group_products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM (
                      SELECT modifier_group_id
                      FROM (
                        SELECT modifier_group_id, tenant_id FROM product_modifier_group_assignments
                        UNION ALL
                        SELECT modifier_group_id, tenant_id FROM addon_group_products
                      ) u
                      GROUP BY modifier_group_id
                      HAVING COUNT(DISTINCT tenant_id) > 1
                    ) conflicts
                  ) THEN
                    RAISE EXCEPTION 'Wave3B migration: same modifier group id is used across multiple tenants; resolve data before applying';
                  END IF;
                END $$;
            ");

            migrationBuilder.Sql($@"
                UPDATE product_modifier_groups AS g
                SET tenant_id = COALESCE(
                  (SELECT a.tenant_id FROM product_modifier_group_assignments AS a
                   WHERE a.modifier_group_id = g.id LIMIT 1),
                  (SELECT ag.tenant_id FROM addon_group_products AS ag
                   WHERE ag.modifier_group_id = g.id LIMIT 1),
                  '{defaultTenantSql}'::uuid
                );
            ");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_product_modifier_groups_id_tenant_id",
                table: "product_modifier_groups",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_modifier_group_id_tenant~",
                table: "product_modifier_group_assignments",
                columns: new[] { "modifier_group_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_product_id_tenant_id",
                table: "product_modifier_group_assignments",
                columns: new[] { "product_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_tenant_id",
                table: "product_modifier_group_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_modifier_group_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "modifier_group_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_product_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "product_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_tenant_id",
                table: "addon_group_products",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_product_modifier_groups_modifier_group~",
                table: "addon_group_products",
                columns: new[] { "modifier_group_id", "tenant_id" },
                principalTable: "product_modifier_groups",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_products_product_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "product_id", "tenant_id" },
                principalTable: "products",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_modifier_group_assignments_product_modifier_groups_~",
                table: "product_modifier_group_assignments",
                columns: new[] { "modifier_group_id", "tenant_id" },
                principalTable: "product_modifier_groups",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_modifier_group_assignments_products_product_id_tena~",
                table: "product_modifier_group_assignments",
                columns: new[] { "product_id", "tenant_id" },
                principalTable: "products",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_addon_group_products_product_modifier_groups_modifier_group~",
                table: "addon_group_products");

            migrationBuilder.DropForeignKey(
                name: "FK_addon_group_products_products_product_id_tenant_id",
                table: "addon_group_products");

            migrationBuilder.DropForeignKey(
                name: "FK_product_modifier_group_assignments_product_modifier_groups_~",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_product_modifier_group_assignments_products_product_id_tena~",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_product_modifier_groups_tenants_tenant_id",
                table: "product_modifier_groups");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_products_id_tenant_id",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_product_modifier_groups_id_tenant_id",
                table: "product_modifier_groups");

            migrationBuilder.DropIndex(
                name: "IX_product_modifier_groups_tenant_id",
                table: "product_modifier_groups");

            migrationBuilder.DropIndex(
                name: "IX_product_modifier_group_assignments_modifier_group_id_tenant~",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropIndex(
                name: "IX_product_modifier_group_assignments_product_id_tenant_id",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropIndex(
                name: "IX_product_modifier_group_assignments_tenant_id",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropIndex(
                name: "IX_addon_group_products_modifier_group_id_tenant_id",
                table: "addon_group_products");

            migrationBuilder.DropIndex(
                name: "IX_addon_group_products_product_id_tenant_id",
                table: "addon_group_products");

            migrationBuilder.DropIndex(
                name: "IX_addon_group_products_tenant_id",
                table: "addon_group_products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "product_modifier_groups");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "product_modifier_group_assignments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "addon_group_products");

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_modifier_group_id",
                table: "product_modifier_group_assignments",
                column: "modifier_group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_product_modifier_groups_modifier_group~",
                table: "addon_group_products",
                column: "modifier_group_id",
                principalTable: "product_modifier_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_products_product_id",
                table: "addon_group_products",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_modifier_group_assignments_product_modifier_groups_~",
                table: "product_modifier_group_assignments",
                column: "modifier_group_id",
                principalTable: "product_modifier_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_modifier_group_assignments_products_product_id",
                table: "product_modifier_group_assignments",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
