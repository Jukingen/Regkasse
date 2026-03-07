using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLegacyModifierIdForMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "legacy_modifier_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_legacy_modifier_id",
                table: "products",
                column: "legacy_modifier_id",
                unique: true,
                filter: "\"legacy_modifier_id\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_modifiers_legacy_modifier_id",
                table: "products",
                column: "legacy_modifier_id",
                principalTable: "product_modifiers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_modifiers_legacy_modifier_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_legacy_modifier_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "legacy_modifier_id",
                table: "products");
        }
    }
}
