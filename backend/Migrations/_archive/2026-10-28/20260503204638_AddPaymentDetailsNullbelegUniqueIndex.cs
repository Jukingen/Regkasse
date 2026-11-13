using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsNullbelegUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_details_nullbeleg_per_register_month",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_year", "rksv_special_receipt_month" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Nullbeleg' AND \"is_active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_details_nullbeleg_per_register_month",
                table: "payment_details");
        }
    }
}
