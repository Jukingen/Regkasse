using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Key/value tenant_settings for feature-flag overrides (and future settings).</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729230000_AddTenantSettings")]
public partial class AddTenantSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_settings", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_settings_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenant_settings_tenant_id_key",
            table: "tenant_settings",
            columns: new[] { "tenant_id", "key" },
            unique: true,
            filter: "tenant_id IS NOT NULL");

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX "IX_tenant_settings_global_key"
            ON tenant_settings (key)
            WHERE tenant_id IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_tenant_settings_key",
            table: "tenant_settings",
            column: "key");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_tenant_settings_global_key";""");
        migrationBuilder.DropTable(name: "tenant_settings");
    }
}
