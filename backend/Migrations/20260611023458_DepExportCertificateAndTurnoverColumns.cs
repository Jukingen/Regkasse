using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class DepExportCertificateAndTurnoverColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "certificate_thumbprint",
                table: "payment_details",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_certificate_thumbprint",
                table: "payment_details",
                column: "certificate_thumbprint",
                filter: "\"certificate_thumbprint\" IS NOT NULL");

            migrationBuilder.AddColumn<string>(
                name: "certificate_thumbprint",
                table: "DailyClosings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_certificate_thumbprint",
                table: "DailyClosings",
                column: "certificate_thumbprint",
                filter: "\"certificate_thumbprint\" IS NOT NULL");

            migrationBuilder.AddColumn<long>(
                name: "last_turnover_counter_cents",
                table: "signature_chain_state",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_certificate_thumbprint",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "certificate_thumbprint",
                table: "payment_details");

            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_certificate_thumbprint",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "certificate_thumbprint",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "last_turnover_counter_cents",
                table: "signature_chain_state");
        }
    }
}
