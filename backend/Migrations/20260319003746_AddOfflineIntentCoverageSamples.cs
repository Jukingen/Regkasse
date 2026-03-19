using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineIntentCoverageSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offline_intent_coverage_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    has_device_id = table.Column<bool>(type: "boolean", nullable: false),
                    has_client_sequence = table.Column<bool>(type: "boolean", nullable: false),
                    replay_batch_correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_intent_coverage_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_CashRegisterId",
                table: "offline_intent_coverage_samples",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_created_at_utc",
                table: "offline_intent_coverage_samples",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_replay_batch_correlation_id",
                table: "offline_intent_coverage_samples",
                column: "replay_batch_correlation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offline_intent_coverage_samples");
        }
    }
}
