using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723190000_AddTenantSettingsHistory")]
public partial class AddTenantSettingsHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "country",
            table: "company_settings",
            type: "character varying(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "AT");

        migrationBuilder.CreateTable(
            name: "tenant_settings_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                setting_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                old_value = table.Column<string>(type: "jsonb", nullable: true),
                new_value = table.Column<string>(type: "jsonb", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending"),
                requested_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                approved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_settings_history", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_settings_history_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tenant_settings_history_AspNetUsers_requested_by",
                    column: x => x.requested_by,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tenant_settings_history_AspNetUsers_approved_by",
                    column: x => x.approved_by,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_settings_history_tenant_id",
            table: "tenant_settings_history",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_tenant_settings_history_status",
            table: "tenant_settings_history",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_tenant_settings_history_requested_at",
            table: "tenant_settings_history",
            column: "requested_at");

        migrationBuilder.CreateIndex(
            name: "idx_tenant_settings_history_tenant_type_status",
            table: "tenant_settings_history",
            columns: new[] { "tenant_id", "setting_type", "status" });

        migrationBuilder.Sql(
            """
            ALTER TABLE tenant_settings_history
              ADD CONSTRAINT ck_tenant_settings_history_setting_type
              CHECK (setting_type IN ('currency', 'country', 'timezone', 'fiscal_settings'));

            ALTER TABLE tenant_settings_history
              ADD CONSTRAINT ck_tenant_settings_history_status
              CHECK (status IN ('pending', 'approved', 'rejected', 'reverted'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE tenant_settings_history DROP CONSTRAINT IF EXISTS ck_tenant_settings_history_setting_type;
            ALTER TABLE tenant_settings_history DROP CONSTRAINT IF EXISTS ck_tenant_settings_history_status;
            """);

        migrationBuilder.DropTable(name: "tenant_settings_history");

        migrationBuilder.DropColumn(
            name: "country",
            table: "company_settings");
    }
}
