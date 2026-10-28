using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_limits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_users_per_register = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    max_active_registers_per_user = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    max_products_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 10000),
                    max_users_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    daily_max_transactions = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    max_transaction_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 10000m),
                    daily_max_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 50000m),
                    max_backups_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    max_backup_size_mb = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    max_offline_transactions = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_limits", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_limits_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_limits_tenant_id",
                table: "tenant_limits",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_limits");
        }
    }
}
