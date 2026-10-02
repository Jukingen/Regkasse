using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEinvoiceValidationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "einvoice_validation_passed",
                table: "invoices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "einvoice_validation_rule_ids",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "einvoice_validation_passed",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "einvoice_validation_rule_ids",
                table: "invoices");
        }
    }
}
