using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717183000_AddOnlineOrderStripePaymentIntent")]
public partial class AddOnlineOrderStripePaymentIntent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "stripe_payment_intent_id",
            table: "online_orders",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "paid_at",
            table: "online_orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_stripe_payment_intent_id",
            table: "online_orders",
            column: "stripe_payment_intent_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_online_orders_stripe_payment_intent_id",
            table: "online_orders");

        migrationBuilder.DropColumn(
            name: "stripe_payment_intent_id",
            table: "online_orders");

        migrationBuilder.DropColumn(
            name: "paid_at",
            table: "online_orders");
    }
}
