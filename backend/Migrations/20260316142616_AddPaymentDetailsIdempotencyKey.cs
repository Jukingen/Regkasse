using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "payment_details",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_idempotency_key",
                table: "payment_details",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_idempotency_key",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "payment_details");
        }
    }
}
