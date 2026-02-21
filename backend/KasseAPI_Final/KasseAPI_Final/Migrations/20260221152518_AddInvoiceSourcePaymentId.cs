using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSourcePaymentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourcePaymentId",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SourcePaymentId",
                table: "invoices",
                column: "SourcePaymentId",
                unique: true,
                filter: "\"SourcePaymentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_SourcePaymentId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SourcePaymentId",
                table: "invoices");
        }
    }
}
