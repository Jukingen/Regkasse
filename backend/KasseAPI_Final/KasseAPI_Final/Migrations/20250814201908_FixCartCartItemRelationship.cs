using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixCartCartItemRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_carts_CartId1",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_products_ProductId",
                table: "cart_items");

            migrationBuilder.DropIndex(
                name: "IX_orders_OrderDate",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_OrderId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_Status",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_items_OrderId",
                table: "order_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_carts",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "IX_carts_CartId",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "IX_carts_Status",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_CartId1",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "CartId1",
                table: "cart_items");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "carts",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_carts",
                table: "carts",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_ApplicationUserId",
                table: "carts",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_carts_CartId",
                table: "cart_items",
                column: "CartId",
                principalTable: "carts",
                principalColumn: "CartId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_products_ProductId",
                table: "cart_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_carts_AspNetUsers_ApplicationUserId",
                table: "carts",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_carts_CartId",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_products_ProductId",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_carts_AspNetUsers_ApplicationUserId",
                table: "carts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_carts",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "IX_carts_ApplicationUserId",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "carts");

            migrationBuilder.AddColumn<Guid>(
                name: "CartId1",
                table: "cart_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_carts",
                table: "carts",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderDate",
                table: "orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderId",
                table: "orders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status",
                table: "orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_CartId",
                table: "carts",
                column: "CartId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carts_Status",
                table: "carts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId1",
                table: "cart_items",
                column: "CartId1");

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_carts_CartId1",
                table: "cart_items",
                column: "CartId1",
                principalTable: "carts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_products_ProductId",
                table: "cart_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
