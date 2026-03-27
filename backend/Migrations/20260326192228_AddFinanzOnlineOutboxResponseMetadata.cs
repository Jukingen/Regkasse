using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanzOnlineOutboxResponseMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReferenceId",
                table: "finanz_online_outbox_messages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatus",
                table: "finanz_online_outbox_messages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResponseJson",
                table: "finanz_online_outbox_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtocolCode",
                table: "finanz_online_outbox_messages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransmissionId",
                table: "finanz_online_outbox_messages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_TransmissionId",
                table: "finanz_online_outbox_messages",
                column: "TransmissionId",
                filter: "\"TransmissionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_finanz_online_outbox_messages_TransmissionId",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ExternalReferenceId",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ExternalStatus",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastResponseJson",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProtocolCode",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "TransmissionId",
                table: "finanz_online_outbox_messages");
        }
    }
}
