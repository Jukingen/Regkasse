using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Optional product → tax_groups FK for flexible MwSt catalog selection.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260724130000_AddProductTaxGroupId")]
public partial class AddProductTaxGroupId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "tax_group_id",
            table: "products",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_products_tax_group_id",
            table: "products",
            column: "tax_group_id");

        migrationBuilder.AddForeignKey(
            name: "FK_products_tax_groups_tax_group_id",
            table: "products",
            column: "tax_group_id",
            principalTable: "tax_groups",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_products_tax_groups_tax_group_id",
            table: "products");

        migrationBuilder.DropIndex(
            name: "IX_products_tax_group_id",
            table: "products");

        migrationBuilder.DropColumn(
            name: "tax_group_id",
            table: "products");
    }
}
