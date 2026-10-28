using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexPaymentDetailsReceiptNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_payment_details_ReceiptNumber",
                table: "payment_details",
                column: "ReceiptNumber",
                unique: true,
                filter: "\"ReceiptNumber\" IS NOT NULL AND \"ReceiptNumber\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_ReceiptNumber",
                table: "payment_details");
        }
    }
}
