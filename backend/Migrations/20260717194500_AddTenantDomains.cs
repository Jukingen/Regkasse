using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717194500_AddTenantDomains")]
public partial class AddTenantDomains : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_domains",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                subdomain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_verified = table.Column<bool>(type: "boolean", nullable: false),
                verification_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_domains", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_domains_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_domains_tenant_id",
            table: "tenant_domains",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_tenant_domains_domain",
            table: "tenant_domains",
            column: "domain",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_tenant_domains_host_lookup",
            table: "tenant_domains",
            columns: new[] { "domain", "is_verified", "is_active" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tenant_domains");
    }
}
