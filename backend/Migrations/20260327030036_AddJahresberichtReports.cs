using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddJahresberichtReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jahresbericht_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViennaYearStart = table.Column<DateTime>(type: "date", nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrectionKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SupersedesReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededByReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LastFinanzOnlineOutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSubmissionStatusCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastSubmissionError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotGrossSalesAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jahresbericht_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jahresbericht_reports_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_CashRegisterId",
                table: "jahresbericht_reports",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_ViennaYearStart_ScopeKind_ReportStatus",
                table: "jahresbericht_reports",
                columns: new[] { "ViennaYearStart", "ScopeKind", "ReportStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jahresbericht_reports");
        }
    }
}
