using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class DropTseSignatureIndexOnInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL B-tree index row limit (~2704 bytes); full JWS compact signatures exceed this — inserts fail.
            migrationBuilder.DropIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback only; may fail if any row stores a signature longer than B-tree max.
            migrationBuilder.CreateIndex(
                name: "IX_invoices_TseSignature",
                table: "invoices",
                column: "TseSignature",
                unique: true,
                filter: "\"TseSignature\" != ''");
        }
    }
}
