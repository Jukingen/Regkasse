using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class MakeTseSignatureIndexPartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices",
                column: "TseSignature",
                unique: true,
                filter: "\"TseSignature\" != ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices",
                column: "TseSignature",
                unique: true);
        }
    }
}
