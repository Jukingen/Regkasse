using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptSequencesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kassen_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sequence_date = table.Column<DateTime>(type: "date", nullable: false),
                    next_sequence = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_sequences", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_sequences_kassen_id_sequence_date",
                table: "receipt_sequences",
                columns: new[] { "kassen_id", "sequence_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_sequences");
        }
    }
}
