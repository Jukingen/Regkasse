using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Safety net: never persist a separate <c>online_payments</c> table.
/// All hosted-payment state lives on <c>gateway_payment_intents</c>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260826170000_AddOnlinePayments")]
public partial class AddOnlinePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS online_payments CASCADE;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: a separate online_payments table is not part of the model.
    }
}
