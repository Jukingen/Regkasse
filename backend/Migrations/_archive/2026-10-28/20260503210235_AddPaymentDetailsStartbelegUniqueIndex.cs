using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsStartbelegUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_cash_register_id",
                table: "payment_details");

            migrationBuilder.CreateIndex(
                name: "ix_payment_details_startbeleg_per_register",
                table: "payment_details",
                column: "cash_register_id",
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Startbeleg' AND \"is_active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_details_startbeleg_per_register",
                table: "payment_details");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cash_register_id",
                table: "payment_details",
                column: "cash_register_id");
        }
    }
}
