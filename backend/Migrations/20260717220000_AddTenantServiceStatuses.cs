using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717220000_AddTenantServiceStatuses")]
public partial class AddTenantServiceStatuses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_service_statuses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                custom_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                deactivation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_service_statuses", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_service_statuses_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_service_statuses_tenant_id",
            table: "tenant_service_statuses",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_tenant_service_statuses_tenant_type",
            table: "tenant_service_statuses",
            columns: new[] { "tenant_id", "service_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_tenant_service_statuses_active_enabled",
            table: "tenant_service_statuses",
            columns: new[] { "is_active", "is_enabled" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tenant_service_statuses");
    }
}
