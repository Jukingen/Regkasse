using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_InvoiceDate",
                table: "invoices");

            // migrationBuilder.DropIndex(
            //     name: "IX_invoices_InvoiceNumber",
            //     table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Status",
                table: "invoices");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "FinanzOnlineSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseStatusCode = table.Column<string>(type: "text", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "jsonb", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanzOnlineSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CompanyName",
                table: "invoices",
                column: "CompanyName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CustomerName",
                table: "invoices",
                column: "CustomerName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceNumber_Trgm",
                table: "invoices",
                column: "InvoiceNumber")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_is_active_InvoiceDate",
                table: "invoices",
                columns: new[] { "is_active", "InvoiceDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status_InvoiceDate",
                table: "invoices",
                columns: new[] { "Status", "InvoiceDate" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanzOnlineSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CompanyName",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CustomerName",
                table: "invoices");

            // migrationBuilder.DropIndex(
            //    name: "IX_invoices_InvoiceNumber",
            //    table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_InvoiceNumber_Trgm",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_is_active_InvoiceDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Status_InvoiceDate",
                table: "invoices");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceDate",
                table: "invoices",
                column: "InvoiceDate");

            // migrationBuilder.CreateIndex(
            //    name: "IX_invoices_InvoiceNumber",
            //    table: "invoices",
            //    column: "InvoiceNumber",
            //    unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status",
                table: "invoices",
                column: "Status");
        }
    }
}
