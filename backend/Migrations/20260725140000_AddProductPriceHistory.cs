using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>RKSV product price history journal and versioned price snapshots.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725140000_AddProductPriceHistory")]
public partial class AddProductPriceHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "product_price_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                new_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                old_tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                new_tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                new_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_rksv_compliant = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                rksv_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                rksv_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_price_history", x => x.id);
                table.ForeignKey(
                    name: "FK_product_price_history_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_product_price_history_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_product_price_history_tax_groups_old_tax_group_id",
                    column: x => x.old_tax_group_id,
                    principalTable: "tax_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_product_price_history_tax_groups_new_tax_group_id",
                    column: x => x.new_tax_group_id,
                    principalTable: "tax_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Aligns with schema: DECIMAL(10,2), ON DELETE CASCADE on product_id, version TEXT,
        // defaults gen_random_uuid()/now(), indexes idx_product_price_versions_*.
        // tenant_id kept for multi-tenant isolation (ITenantEntity + global query filters).
        migrationBuilder.CreateTable(
            name: "product_price_versions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                version = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_price_versions", x => x.id);
                table.ForeignKey(
                    name: "FK_product_price_versions_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_product_price_versions_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_product_price_versions_tax_groups_tax_group_id",
                    column: x => x.tax_group_id,
                    principalTable: "tax_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_tenant_id_effective_from",
            table: "product_price_history",
            columns: new[] { "tenant_id", "effective_from" });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_tenant_id_product_id_effective_from",
            table: "product_price_history",
            columns: new[] { "tenant_id", "product_id", "effective_from" });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_tenant_id_product_id_is_active",
            table: "product_price_history",
            columns: new[] { "tenant_id", "product_id", "is_active" });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_product_id",
            table: "product_price_history",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_old_tax_group_id",
            table: "product_price_history",
            column: "old_tax_group_id");

        migrationBuilder.CreateIndex(
            name: "IX_product_price_history_new_tax_group_id",
            table: "product_price_history",
            column: "new_tax_group_id");

        migrationBuilder.CreateIndex(
            name: "idx_product_price_versions_product_id",
            table: "product_price_versions",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "idx_product_price_versions_is_current",
            table: "product_price_versions",
            column: "is_current");

        migrationBuilder.CreateIndex(
            name: "IX_product_price_versions_tenant_id_product_id_valid_from",
            table: "product_price_versions",
            columns: new[] { "tenant_id", "product_id", "valid_from" });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_versions_tenant_id_product_id_is_current",
            table: "product_price_versions",
            columns: new[] { "tenant_id", "product_id", "is_current" });

        migrationBuilder.CreateIndex(
            name: "IX_product_price_versions_tax_group_id",
            table: "product_price_versions",
            column: "tax_group_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "product_price_history");
        migrationBuilder.DropTable(name: "product_price_versions");
    }
}
