using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddMonatsbelegLateCreationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "rksv_is_late_created",
                table: "payment_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rksv_late_creation_reason",
                table: "payment_details",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rksv_intended_period_date",
                table: "payment_details",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rksv_is_late_created",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "rksv_late_creation_reason",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "rksv_intended_period_date",
                table: "payment_details");
        }
    }
}
