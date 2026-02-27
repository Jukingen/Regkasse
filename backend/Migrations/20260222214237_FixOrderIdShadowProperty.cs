using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderIdShadowProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_OrderId1",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "IX_order_items_OrderId1",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "OrderId1",
                table: "order_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId1",
                table: "order_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId1",
                table: "order_items",
                column: "OrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_OrderId1",
                table: "order_items",
                column: "OrderId1",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
