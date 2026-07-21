using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717181000_AddOnlineOrderPosCartLink")]
public partial class AddOnlineOrderPosCartLink : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "pos_cart_id",
            table: "online_orders",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "pushed_to_pos_at",
            table: "online_orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_pos_cart_id",
            table: "online_orders",
            column: "pos_cart_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_online_orders_pos_cart_id",
            table: "online_orders");

        migrationBuilder.DropColumn(
            name: "pos_cart_id",
            table: "online_orders");

        migrationBuilder.DropColumn(
            name: "pushed_to_pos_at",
            table: "online_orders");
    }
}
