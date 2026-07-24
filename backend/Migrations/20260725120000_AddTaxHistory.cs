using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Append-only product tax rate / tax group change journal.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725120000_AddTaxHistory")]
public partial class AddTaxHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tax_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                new_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                invoice_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tax_history", x => x.id);
                table.ForeignKey(
                    name: "FK_tax_history_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tax_history_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tax_history_tax_groups_tax_group_id",
                    column: x => x.tax_group_id,
                    principalTable: "tax_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tax_history_tenant_id_changed_at",
            table: "tax_history",
            columns: new[] { "tenant_id", "changed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_tax_history_tenant_id_product_id_changed_at",
            table: "tax_history",
            columns: new[] { "tenant_id", "product_id", "changed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_tax_history_product_id",
            table: "tax_history",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "IX_tax_history_tax_group_id",
            table: "tax_history",
            column: "tax_group_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tax_history");
    }
}
