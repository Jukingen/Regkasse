using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260817120000_AddSubscriptionInvoicePaymentFields")]
public partial class AddSubscriptionInvoicePaymentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "paid_at_utc",
            table: "subscription_invoices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "payment_method",
            table: "subscription_invoices",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "payment_reference",
            table: "subscription_invoices",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "void_reason",
            table: "subscription_invoices",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "voided_at_utc",
            table: "subscription_invoices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "email_sent_at_utc",
            table: "subscription_invoices",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "paid_at_utc", table: "subscription_invoices");
        migrationBuilder.DropColumn(name: "payment_method", table: "subscription_invoices");
        migrationBuilder.DropColumn(name: "payment_reference", table: "subscription_invoices");
        migrationBuilder.DropColumn(name: "void_reason", table: "subscription_invoices");
        migrationBuilder.DropColumn(name: "voided_at_utc", table: "subscription_invoices");
        migrationBuilder.DropColumn(name: "email_sent_at_utc", table: "subscription_invoices");
    }
}
