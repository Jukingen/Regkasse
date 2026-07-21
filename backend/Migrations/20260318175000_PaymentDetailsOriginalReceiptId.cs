using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class PaymentDetailsOriginalReceiptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "original_receipt_id",
                table: "payment_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_original_receipt_id",
                table: "payment_details",
                column: "original_receipt_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_details_receipts_original_receipt_id",
                table: "payment_details",
                column: "original_receipt_id",
                principalTable: "receipts",
                principalColumn: "receipt_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_details_receipts_original_receipt_id",
                table: "payment_details");

            migrationBuilder.DropIndex(
                name: "IX_payment_details_original_receipt_id",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "original_receipt_id",
                table: "payment_details");
        }
    }
}
