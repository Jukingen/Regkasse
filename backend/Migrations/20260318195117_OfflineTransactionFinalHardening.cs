using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class OfflineTransactionFinalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "client_sequence_number",
                table: "offline_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "clock_drift_warning",
                table: "offline_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "offline_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payload_hash",
                table: "offline_transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sequence_duplicate_detected",
                table: "offline_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "sequence_gap_detected",
                table: "offline_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "server_received_at_utc",
                table: "offline_transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId_device_id_client_sequen~",
                table: "offline_transactions",
                columns: new[] { "CashRegisterId", "device_id", "client_sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId_payload_hash",
                table: "offline_transactions",
                columns: new[] { "CashRegisterId", "payload_hash" },
                unique: true);

            // Best-effort backfill for legacy rows:
            // - server_received_at_utc: fall back to created_at (BaseEntity timestamp)
            // - payload_hash: sha256(payload_json::text) (requires pgcrypto for digest)
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql(
                "UPDATE offline_transactions SET server_received_at_utc = created_at WHERE server_received_at_utc = '0001-01-01T00:00:00Z';");
            migrationBuilder.Sql(
                "UPDATE offline_transactions SET payload_hash = encode(digest(\"PayloadJson\"::text, 'sha256'), 'hex') WHERE payload_hash IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_offline_transactions_CashRegisterId_device_id_client_sequen~",
                table: "offline_transactions");

            migrationBuilder.DropIndex(
                name: "IX_offline_transactions_CashRegisterId_payload_hash",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "client_sequence_number",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "clock_drift_warning",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "payload_hash",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "sequence_duplicate_detected",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "sequence_gap_detected",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "server_received_at_utc",
                table: "offline_transactions");
        }
    }
}
