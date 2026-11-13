using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    public partial class AddPeriodenberichtRuns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "periodenbericht_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodPreset = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PeriodStartLocalDate = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEndLocalDate = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashierId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    PaymentMethodFilter = table.Column<int>(type: "integer", nullable: true),
                    ActiveOnly = table.Column<bool>(type: "boolean", nullable: false),
                    QueryParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    QueryParametersHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentRowCount = table.Column<int>(type: "integer", nullable: false),
                    GrossTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundRowCount = table.Column<int>(type: "integer", nullable: false),
                    RefundAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ExportProfileKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodenbericht_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_periodenbericht_runs_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_CashRegisterId",
                table: "periodenbericht_runs",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_CreatedAtUtc",
                table: "periodenbericht_runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_PeriodStartLocalDate_PeriodEndLocalDate_ScopeK~",
                table: "periodenbericht_runs",
                columns: new[] { "PeriodStartLocalDate", "PeriodEndLocalDate", "ScopeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_QueryParametersHash",
                table: "periodenbericht_runs",
                column: "QueryParametersHash");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "periodenbericht_runs");
        }
    }
}
