using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanzOnlineSoapParticipantIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinanzOnlineHerstellerId",
                table: "company_settings",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanzOnlineTelematikId",
                table: "company_settings",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinanzOnlineHerstellerId",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineTelematikId",
                table: "company_settings");
        }
    }
}
