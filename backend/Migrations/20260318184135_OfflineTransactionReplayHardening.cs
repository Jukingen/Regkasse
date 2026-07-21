using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class OfflineTransactionReplayHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FiscalizedAtUtc",
                table: "offline_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "offline_transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessageSafe",
                table: "offline_transactions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReplayAttemptAt",
                table: "offline_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineCreatedAtUtc",
                table: "offline_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE offline_transactions SET "OfflineCreatedAtUtc" = created_at
                WHERE "OfflineCreatedAtUtc" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OfflineCreatedAtUtc",
                table: "offline_transactions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "offline_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FiscalizedAtUtc",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "LastErrorMessageSafe",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "LastReplayAttemptAt",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "OfflineCreatedAtUtc",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "offline_transactions");
        }
    }
}
