using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPeppolParticipantIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "eidentifier_scheme",
                table: "peppol_participants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eidentifier_value",
                table: "peppol_participants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_entity_id",
                table: "peppol_participants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "eidentifier_scheme",
                table: "peppol_participants");

            migrationBuilder.DropColumn(
                name: "eidentifier_value",
                table: "peppol_participants");

            migrationBuilder.DropColumn(
                name: "legal_entity_id",
                table: "peppol_participants");
        }
    }
}
