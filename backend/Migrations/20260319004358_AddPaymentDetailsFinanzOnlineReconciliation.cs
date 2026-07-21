using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsFinanzOnlineReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finanz_online_error",
                table: "payment_details",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "finanz_online_last_attempt_at_utc",
                table: "payment_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finanz_online_reference_id",
                table: "payment_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "finanz_online_retry_count",
                table: "payment_details",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "finanz_online_status",
                table: "payment_details",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_finanz_online_status",
                table: "payment_details",
                column: "finanz_online_status",
                filter: "\"finanz_online_status\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_finanz_online_status",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "finanz_online_error",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "finanz_online_last_attempt_at_utc",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "finanz_online_reference_id",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "finanz_online_retry_count",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "finanz_online_status",
                table: "payment_details");
        }
    }
}
