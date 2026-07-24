using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723320000_AddTseAutoScaling")]
public partial class AddTseAutoScaling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_scaling_policies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                min_devices = table.Column<int>(type: "integer", nullable: false),
                max_devices = table.Column<int>(type: "integer", nullable: false),
                target_transactions_per_device = table.Column<int>(type: "integer", nullable: false),
                scale_up_threshold = table.Column<double>(type: "double precision", nullable: false),
                scale_down_threshold = table.Column<double>(type: "double precision", nullable: false),
                cooldown_minutes = table.Column<int>(type: "integer", nullable: false),
                auto_provision = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_scaling_policies", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_scaling_policies_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tse_scaling_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                from_devices = table.Column<int>(type: "integer", nullable: false),
                to_devices = table.Column<int>(type: "integer", nullable: false),
                load_percent = table.Column<double>(type: "double precision", nullable: false),
                applied = table.Column<bool>(type: "boolean", nullable: false),
                simulation_only = table.Column<bool>(type: "boolean", nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_scaling_history", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_scaling_history_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_scaling_policies_tenant",
            table: "tse_scaling_policies",
            column: "tenant_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_tse_scaling_history_tenant_evaluated",
            table: "tse_scaling_history",
            columns: new[] { "tenant_id", "evaluated_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_scaling_history");
        migrationBuilder.DropTable(name: "tse_scaling_policies");
    }
}
