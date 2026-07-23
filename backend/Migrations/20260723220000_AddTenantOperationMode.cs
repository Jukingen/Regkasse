using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723220000_AddTenantOperationMode")]
public partial class AddTenantOperationMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "operation_mode",
            table: "tenants",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "active");

        migrationBuilder.AddColumn<string>(
            name: "maintenance_message",
            table: "tenants",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "maintenance_started_at",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "maintenance_ends_at",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_tenants_operation_mode",
            table: "tenants",
            column: "operation_mode");

        migrationBuilder.Sql(
            """
            ALTER TABLE tenants
              ADD CONSTRAINT ck_tenants_operation_mode
              CHECK (operation_mode IN ('active', 'readonly', 'maintenance'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE tenants DROP CONSTRAINT IF EXISTS ck_tenants_operation_mode;
            """);

        migrationBuilder.DropIndex(
            name: "idx_tenants_operation_mode",
            table: "tenants");

        migrationBuilder.DropColumn(name: "maintenance_ends_at", table: "tenants");
        migrationBuilder.DropColumn(name: "maintenance_started_at", table: "tenants");
        migrationBuilder.DropColumn(name: "maintenance_message", table: "tenants");
        migrationBuilder.DropColumn(name: "operation_mode", table: "tenants");
    }
}
