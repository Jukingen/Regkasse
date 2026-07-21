using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717180000_AddOnlineOrders")]
public partial class AddOnlineOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "online_orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                customer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                customer_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                order_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                table_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                delivery_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                tax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                order_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "web")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_online_orders", x => x.id);
                table.ForeignKey(
                    name: "FK_online_orders_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "online_order_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                online_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_online_order_items", x => x.id);
                table.ForeignKey(
                    name: "FK_online_order_items_online_orders_online_order_id",
                    column: x => x.online_order_id,
                    principalTable: "online_orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "online_order_item_modifiers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                online_order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_online_order_item_modifiers", x => x.id);
                table.ForeignKey(
                    name: "FK_online_order_item_modifiers_online_order_items_online_order_item_id",
                    column: x => x.online_order_item_id,
                    principalTable: "online_order_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_tenant_id",
            table: "online_orders",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_order_status",
            table: "online_orders",
            column: "order_status");

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_payment_status",
            table: "online_orders",
            column: "payment_status");

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_created_at",
            table: "online_orders",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_tenant_order_number",
            table: "online_orders",
            columns: new[] { "tenant_id", "order_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_tenant_status_created",
            table: "online_orders",
            columns: new[] { "tenant_id", "order_status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_online_order_items_order_id",
            table: "online_order_items",
            column: "online_order_id");

        migrationBuilder.CreateIndex(
            name: "idx_online_order_items_product_id",
            table: "online_order_items",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "idx_online_order_item_modifiers_item_id",
            table: "online_order_item_modifiers",
            column: "online_order_item_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "online_order_item_modifiers");
        migrationBuilder.DropTable(name: "online_order_items");
        migrationBuilder.DropTable(name: "online_orders");
    }
}
