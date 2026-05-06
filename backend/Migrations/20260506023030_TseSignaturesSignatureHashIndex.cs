using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class TseSignaturesSignatureHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TseSignatures_Signature",
                table: "TseSignatures");

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_Signature_Hash",
                table: "TseSignatures",
                column: "Signature")
                .Annotation("Npgsql:IndexMethod", "hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TseSignatures_Signature_Hash",
                table: "TseSignatures");

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_Signature",
                table: "TseSignatures",
                column: "Signature",
                unique: true);
        }
    }
}
