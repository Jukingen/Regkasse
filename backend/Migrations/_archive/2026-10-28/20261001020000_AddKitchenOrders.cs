using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "AK_carts_Id",
                table: "carts",
                column: "id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "kitchen_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    table_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ready_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    served_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_kitchen_orders_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kitchen_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kitchen_orders_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "kitchen_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    kitchen_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_kitchen_order_items_kitchen_orders_kitchen_order_id",
                        column: x => x.kitchen_order_id,
                        principalTable: "kitchen_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kitchen_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_tenant_id",
                table: "kitchen_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_cash_register_id",
                table: "kitchen_orders",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_status",
                table: "kitchen_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_cart_id",
                table: "kitchen_orders",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_order_items_kitchen_order_id",
                table: "kitchen_order_items",
                column: "kitchen_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_order_items_product_id",
                table: "kitchen_order_items",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kitchen_order_items");

            migrationBuilder.DropTable(
                name: "kitchen_orders");

            migrationBuilder.DropIndex(
                name: "AK_carts_Id",
                table: "carts");
        }
    }
}
