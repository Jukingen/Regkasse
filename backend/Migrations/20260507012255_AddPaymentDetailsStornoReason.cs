using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailsStornoReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "storno_reason",
                table: "payment_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "time_sync_warning",
                table: "payment_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "system_time_sync_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sync_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    system_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ntp_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    offset_seconds = table.Column<double>(type: "double precision", nullable: false),
                    ntp_server_used = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_time_sync_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_time_sync_logs_sync_time_utc",
                table: "system_time_sync_logs",
                column: "sync_time_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_time_sync_logs");

            migrationBuilder.DropColumn(
                name: "storno_reason",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "time_sync_warning",
                table: "payment_details");
        }
    }
}
