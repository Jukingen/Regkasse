using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeReportLifecycleChains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionOfReportId",
                table: "tagesbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionType",
                table: "tagesbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalReportId",
                table: "tagesbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RebuildCause",
                table: "tagesbericht_reports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportRevisionReason",
                table: "tagesbericht_reports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportVersion",
                table: "tagesbericht_reports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionImpact",
                table: "tagesbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionOfReportId",
                table: "monatsbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionType",
                table: "monatsbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalReportId",
                table: "monatsbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RebuildCause",
                table: "monatsbericht_reports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportRevisionReason",
                table: "monatsbericht_reports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportVersion",
                table: "monatsbericht_reports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionImpact",
                table: "monatsbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionOfReportId",
                table: "jahresbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionType",
                table: "jahresbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalReportId",
                table: "jahresbericht_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RebuildCause",
                table: "jahresbericht_reports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportRevisionReason",
                table: "jahresbericht_reports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportVersion",
                table: "jahresbericht_reports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionImpact",
                table: "jahresbericht_reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_tagesbericht_reports_OriginalReportId_ReportVersion",
                table: "tagesbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_monatsbericht_reports_OriginalReportId_ReportVersion",
                table: "monatsbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_OriginalReportId_ReportVersion",
                table: "jahresbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tagesbericht_reports_OriginalReportId_ReportVersion",
                table: "tagesbericht_reports");

            migrationBuilder.DropIndex(
                name: "IX_monatsbericht_reports_OriginalReportId_ReportVersion",
                table: "monatsbericht_reports");

            migrationBuilder.DropIndex(
                name: "IX_jahresbericht_reports_OriginalReportId_ReportVersion",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionOfReportId",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionType",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "OriginalReportId",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "RebuildCause",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportRevisionReason",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportVersion",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "SubmissionImpact",
                table: "tagesbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionOfReportId",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionType",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "OriginalReportId",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "RebuildCause",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportRevisionReason",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportVersion",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "SubmissionImpact",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionOfReportId",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "CorrectionType",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "OriginalReportId",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "RebuildCause",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportRevisionReason",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "ReportVersion",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "SubmissionImpact",
                table: "jahresbericht_reports");
        }
    }
}
