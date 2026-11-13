using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Aligns the live database with the EF model after the 76-d snapshot sync.
/// <c>backup_runs.download_count</c> already defaults to 0 on kasse_db; SET DEFAULT is idempotent.
/// <c>payment_details."CustomerId"</c> was ON DELETE NO ACTION (omitted in
/// <c>20250814051845_FixRelationshipMappings</c>). The model is Restrict.
/// </summary>
public partial class AlignBackupRunsDownloadCountAndPaymentDetailsFk : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE backup_runs ALTER COLUMN download_count SET DEFAULT 0;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_payment_details_customers_CustomerId",
            table: "payment_details");

        migrationBuilder.AddForeignKey(
            name: "FK_payment_details_customers_CustomerId",
            table: "payment_details",
            column: "CustomerId",
            principalTable: "customers",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_payment_details_customers_CustomerId",
            table: "payment_details");

        migrationBuilder.AddForeignKey(
            name: "FK_payment_details_customers_CustomerId",
            table: "payment_details",
            column: "CustomerId",
            principalTable: "customers",
            principalColumn: "id",
            onDelete: ReferentialAction.NoAction);
    }
}
