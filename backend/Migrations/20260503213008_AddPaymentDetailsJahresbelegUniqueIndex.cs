using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsJahresbelegUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_details_jahresbeleg_per_register_year",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_year" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Jahresbeleg' AND \"is_active\" = true AND \"rksv_special_receipt_year\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_details_jahresbeleg_per_register_year",
                table: "payment_details");
        }
    }
}
