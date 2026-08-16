using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Stores Fiskaly SIGN AT SCU id and TSE provisioning outcome on the tenant row
/// so Super Admin can see whether create-tenant used Fiskaly or Soft TSE fallback.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260816163000_AddTenantTseProvisioningColumns")]
public partial class AddTenantTseProvisioningColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "tse_scu_id",
            table: "tenants",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "tse_status",
            table: "tenants",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "tse_provisioned_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "tse_scu_id", table: "tenants");
        migrationBuilder.DropColumn(name: "tse_status", table: "tenants");
        migrationBuilder.DropColumn(name: "tse_provisioned_at_utc", table: "tenants");
    }
}
