using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddReportPdfsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_pdfs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by_user_id = table.Column<string>(type: "text", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_pdfs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_pdfs_tenant_id",
                table: "report_pdfs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_pdfs_tenant_id_report_type_report_id_language",
                table: "report_pdfs",
                columns: new[] { "tenant_id", "report_type", "report_id", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_pdfs");
        }
    }
}
