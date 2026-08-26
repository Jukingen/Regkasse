using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Drops leftover <c>online_payments</c> if a previous bad migration created it.
/// Persistence remains <c>gateway_payment_intents</c>. Idempotent.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260826180000_DropOnlinePaymentsIfExists")]
public partial class DropOnlinePaymentsIfExists : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS online_payments CASCADE;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: the table is not part of the model.
    }
}
