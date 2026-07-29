using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Append-only DEP export lifecycle audit trail.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725200000_AddDepExportAuditEntries")]
public partial class AddDepExportAuditEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dep_export_audit_entries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                export_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                export_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                user_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                action_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                details = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dep_export_audit_entries", x => x.id);
                table.ForeignKey(
                    name: "FK_dep_export_audit_entries_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_audit_entries_tenant_id_action_at",
            table: "dep_export_audit_entries",
            columns: new[] { "tenant_id", "action_at" });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_audit_entries_tenant_id_action",
            table: "dep_export_audit_entries",
            columns: new[] { "tenant_id", "action" });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_audit_entries_export_history_id",
            table: "dep_export_audit_entries",
            column: "export_history_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dep_export_audit_entries");
    }
}
