using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsRksvSpecialReceiptColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "rksv_nullbeleg_acts_as_jahresbeleg",
                table: "payment_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rksv_special_receipt_kind",
                table: "payment_details",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rksv_special_receipt_month",
                table: "payment_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rksv_special_receipt_year",
                table: "payment_details",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rksv_nullbeleg_acts_as_jahresbeleg",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "rksv_special_receipt_kind",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "rksv_special_receipt_month",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "rksv_special_receipt_year",
                table: "payment_details");
        }
    }
}
