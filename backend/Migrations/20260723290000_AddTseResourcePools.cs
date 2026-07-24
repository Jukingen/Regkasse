using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723290000_AddTseResourcePools")]
public partial class AddTseResourcePools : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_resource_pools",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                pool_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                total_capacity = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_resource_pools", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tse_resource_pool_assignments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                reserved_capacity = table.Column<int>(type: "integer", nullable: false),
                assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                assigned_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_resource_pool_assignments", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_resource_pool_assignments_pools",
                    column: x => x.pool_id,
                    principalTable: "tse_resource_pools",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tse_resource_pool_assignments_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tse_resource_pool_rules",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                rule_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                rule_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_resource_pool_rules", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_resource_pool_rules_pools",
                    column: x => x.pool_id,
                    principalTable: "tse_resource_pools",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_resource_pools_name",
            table: "tse_resource_pools",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "idx_tse_resource_pools_type",
            table: "tse_resource_pools",
            column: "pool_type");

        migrationBuilder.CreateIndex(
            name: "idx_tse_resource_pool_assignments_pool_id",
            table: "tse_resource_pool_assignments",
            column: "pool_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_resource_pool_assignments_tenant_id",
            table: "tse_resource_pool_assignments",
            column: "tenant_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_tse_resource_pool_rules_pool_id",
            table: "tse_resource_pool_rules",
            column: "pool_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_resource_pool_assignments");
        migrationBuilder.DropTable(name: "tse_resource_pool_rules");
        migrationBuilder.DropTable(name: "tse_resource_pools");
    }
}
