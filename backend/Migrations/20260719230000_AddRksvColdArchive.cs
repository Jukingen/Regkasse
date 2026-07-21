using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260719230000_AddRksvColdArchive")]
public partial class AddRksvColdArchive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rksv_cold_archive_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                cutoff_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                archive_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                payment_count = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rksv_cold_archive_runs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "rksv_cold_archive_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                archive_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rksv_cold_archive_items", x => x.id);
                table.ForeignKey(
                    name: "FK_rksv_cold_archive_items_rksv_cold_archive_runs_archive_run_id",
                    column: x => x.archive_run_id,
                    principalTable: "rksv_cold_archive_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_rksv_cold_archive_items_payment_detail_id",
            table: "rksv_cold_archive_items",
            column: "payment_detail_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_rksv_cold_archive_items_archive_run_id",
            table: "rksv_cold_archive_items",
            column: "archive_run_id");

        migrationBuilder.CreateIndex(
            name: "idx_rksv_cold_archive_items_tenant_id",
            table: "rksv_cold_archive_items",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_rksv_cold_archive_runs_created_at",
            table: "rksv_cold_archive_runs",
            column: "created_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rksv_cold_archive_items");
        migrationBuilder.DropTable(name: "rksv_cold_archive_runs");
    }
}
