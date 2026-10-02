using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
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
