using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717190000_AddOnlineCheckoutPaymentMethods")]
public partial class AddOnlineCheckoutPaymentMethods : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "online_checkout_payment_methods",
            table: "system_settings",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            defaultValue: "card,cash,online");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "online_checkout_payment_methods",
            table: "system_settings");
    }
}
