using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260723010000_AddExportEmailDeliveries")]
public partial class AddExportEmailDeliveries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "export_email_deliveries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                message = table.Column<string>(type: "text", nullable: true),
                file_name = table.Column<string>(type: "text", nullable: false),
                content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                delivery_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                source_id = table.Column<Guid>(type: "uuid", nullable: true),
                download_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                download_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                artifact_relative_path = table.Column<string>(type: "text", nullable: true),
                scheduled_for_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_export_email_deliveries", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_export_email_deliveries_tenant_created",
            table: "export_email_deliveries",
            columns: new[] { "tenant_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_export_email_deliveries_tenant_status",
            table: "export_email_deliveries",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_export_email_deliveries_token_hash",
            table: "export_email_deliveries",
            column: "download_token_hash",
            unique: true,
            filter: "download_token_hash IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_export_email_deliveries_scheduled",
            table: "export_email_deliveries",
            columns: new[] { "status", "scheduled_for_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "export_email_deliveries");
    }
}
