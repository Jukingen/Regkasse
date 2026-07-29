using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// RKSV catalog versioning: archive superseded products and link successors via original_product_id.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725150000_AddProductCatalogVersioning")]
public partial class AddProductCatalogVersioning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "version",
            table: "products",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<Guid>(
            name: "original_product_id",
            table: "products",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "archived_at",
            table: "products",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_products_original_product_id",
            table: "products",
            column: "original_product_id");

        migrationBuilder.CreateIndex(
            name: "IX_products_tenant_id_original_product_id_version",
            table: "products",
            columns: new[] { "tenant_id", "original_product_id", "version" });

        migrationBuilder.AddForeignKey(
            name: "FK_products_products_original_product_id",
            table: "products",
            column: "original_product_id",
            principalTable: "products",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_products_products_original_product_id",
            table: "products");

        migrationBuilder.DropIndex(
            name: "IX_products_original_product_id",
            table: "products");

        migrationBuilder.DropIndex(
            name: "IX_products_tenant_id_original_product_id_version",
            table: "products");

        migrationBuilder.DropColumn(name: "version", table: "products");
        migrationBuilder.DropColumn(name: "original_product_id", table: "products");
        migrationBuilder.DropColumn(name: "archived_at", table: "products");
    }
}
