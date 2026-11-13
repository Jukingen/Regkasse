using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddFormalReportUpstreamReviewFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpstreamReviewReasonCode",
                table: "monatsbericht_reports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpstreamReviewRequired",
                table: "monatsbericht_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamReviewReasonCode",
                table: "jahresbericht_reports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpstreamReviewRequired",
                table: "jahresbericht_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpstreamReviewReasonCode",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "UpstreamReviewRequired",
                table: "monatsbericht_reports");

            migrationBuilder.DropColumn(
                name: "UpstreamReviewReasonCode",
                table: "jahresbericht_reports");

            migrationBuilder.DropColumn(
                name: "UpstreamReviewRequired",
                table: "jahresbericht_reports");
        }
    }
}
