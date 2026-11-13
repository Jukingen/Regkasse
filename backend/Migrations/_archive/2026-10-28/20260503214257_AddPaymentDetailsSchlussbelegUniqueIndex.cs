using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsSchlussbelegUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_details_schlussbeleg_per_register",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_kind" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Schlussbeleg' AND \"is_active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_details_schlussbeleg_per_register",
                table: "payment_details");
        }
    }
}
