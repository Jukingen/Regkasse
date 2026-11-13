using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class SyncKitchenAndGuestFolioIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_kitchen_order_items_product_id",
                table: "kitchen_order_items",
                newName: "IX_kitchen_order_items_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_room_id",
                table: "guest_folios",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_guest_folios_room_id",
                table: "guest_folios");

            migrationBuilder.RenameIndex(
                name: "IX_kitchen_order_items_product_id",
                table: "kitchen_order_items",
                newName: "ix_kitchen_order_items_product_id");
        }
    }
}
