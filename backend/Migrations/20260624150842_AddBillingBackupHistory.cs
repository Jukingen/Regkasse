using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingBackupHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_backup_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    backup_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    backup_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "success"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    triggered_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retention_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_backup_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_backup_history_license_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "license_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_backup_run_id",
                table: "billing_backup_history",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_retention",
                table: "billing_backup_history",
                column: "retention_until_utc");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_sale_id",
                table: "billing_backup_history",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_status",
                table: "billing_backup_history",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_backup_history");
        }
    }
}
