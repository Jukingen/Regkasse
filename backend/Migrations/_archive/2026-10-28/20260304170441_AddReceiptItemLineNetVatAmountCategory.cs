using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptItemLineNetVatAmountCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category_name",
                table: "receipt_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "line_net",
                table: "receipt_items",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "receipt_items",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill: line_net = ROUND(total_price / (1 + tax_rate/100), 2), vat_amount = total_price - line_net
            migrationBuilder.Sql(@"
                UPDATE receipt_items
                SET line_net = ROUND(total_price / NULLIF(1 + tax_rate / 100, 0), 2),
                    vat_amount = total_price - ROUND(total_price / NULLIF(1 + tax_rate / 100, 0), 2)
                WHERE line_net = 0 AND vat_amount = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category_name",
                table: "receipt_items");

            migrationBuilder.DropColumn(
                name: "line_net",
                table: "receipt_items");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "receipt_items");
        }
    }
}
