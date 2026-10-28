using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanzOnlineOutboxResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionReason",
                table: "finanz_online_outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "finanz_online_outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "finanz_online_outbox_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionReason",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "finanz_online_outbox_messages");
        }
    }
}
