using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class HardenFinanzOnlineRetryTaxonomyAndLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "finanz_online_outbox_messages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "finanz_online_outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingToken",
                table: "finanz_online_outbox_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_TenantId_BranchId_MessageType~",
                table: "finanz_online_outbox_messages",
                columns: new[] { "TenantId", "BranchId", "MessageType", "BusinessKey", "PayloadHash", "Mode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_finanz_online_outbox_messages_TenantId_BranchId_MessageType~",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "finanz_online_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                table: "finanz_online_outbox_messages");
        }
    }
}
