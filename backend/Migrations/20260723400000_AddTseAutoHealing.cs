using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723400000_AddTseAutoHealing")]
public partial class AddTseAutoHealing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_healing_configurations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                max_auto_heal_attempts = table.Column<int>(type: "integer", nullable: false),
                cooldown_minutes = table.Column<int>(type: "integer", nullable: false),
                notify_on_heal = table.Column<bool>(type: "boolean", nullable: false),
                allow_auto_failover = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_healing_configurations", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_healing_configurations_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_healing_configurations_tenant",
            table: "tse_healing_configurations",
            column: "tenant_id",
            unique: true);

        migrationBuilder.CreateTable(
            name: "tse_healing_rules",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                last_triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_healing_rules", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_healing_rules_config",
                    column: x => x.configuration_id,
                    principalTable: "tse_healing_configurations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_healing_rules_config",
            table: "tse_healing_rules",
            column: "configuration_id");

        migrationBuilder.CreateTable(
            name: "tse_healing_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                applied = table.Column<bool>(type: "boolean", nullable: false),
                health_score_before = table.Column<int>(type: "integer", nullable: false),
                health_score_after = table.Column<int>(type: "integer", nullable: true),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_healing_history", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_healing_history_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tse_healing_history_devices",
                    column: x => x.device_id,
                    principalTable: "TseDevices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_healing_history_tenant_started",
            table: "tse_healing_history",
            columns: new[] { "tenant_id", "started_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_healing_history_device_started",
            table: "tse_healing_history",
            columns: new[] { "device_id", "started_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_healing_history");
        migrationBuilder.DropTable(name: "tse_healing_rules");
        migrationBuilder.DropTable(name: "tse_healing_configurations");
    }
}
