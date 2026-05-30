using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class ApplyOrphanLocalSchemaChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "session_idle_timeout_enabled",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "max_stock_level",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "backup_schedule_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_schedule_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_backup_schedule_configurations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backup_schedule_configurations_tenant_id",
                table: "backup_schedule_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO backup_schedule_configurations (
                    id, tenant_id, enabled, schedule_cron, retention_days,
                    last_run_at, next_run_at, created_at, updated_at, is_active)
                SELECT
                    gen_random_uuid(),
                    t.id,
                    COALESCE(bs.enabled, false),
                    COALESCE(bs.schedule_cron, '0 2 * * *'),
                    COALESCE(bs.retention_days, 30),
                    bs.last_run_at,
                    bs.next_run_at,
                    NOW() AT TIME ZONE 'UTC',
                    NOW() AT TIME ZONE 'UTC',
                    true
                FROM tenants t
                LEFT JOIN backup_settings bs ON bs.id = 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM backup_schedule_configurations c WHERE c.tenant_id = t.id);
                """);

            migrationBuilder.Sql(
                """
                UPDATE categories
                SET
                    is_system_category = true,
                    original_demo_name = CASE category_key
                        WHEN 'salate' THEN 'Salate'
                        WHEN 'stangerl' THEN 'Stangerl'
                        WHEN 'baguettes' THEN 'Baguettes'
                        WHEN 'calzone' THEN 'Calzone'
                        WHEN 'pizza-mittel' THEN 'Pizza, mittel'
                        WHEN 'pizza-partner' THEN 'Pizza, Partner'
                        WHEN 'familien-pizza' THEN 'Familien-Pizza'
                        WHEN 'mexikanische-pizza-mittel' THEN 'Mexikanische Pizza, mittel'
                        WHEN 'mexikanische-pizza-partner' THEN 'Mexikanische Pizza, Partner'
                        WHEN 'pasta' THEN 'Pasta'
                        WHEN 'imbiss' THEN 'Imbiss'
                        WHEN 'burger' THEN 'Burger'
                        WHEN 'kebap' THEN 'Kebap'
                        WHEN 'desserts' THEN 'Desserts'
                        WHEN 'saucen' THEN 'Saucen'
                        WHEN 'alkoholfreie-getranke' THEN 'Alkoholfreie Getränke'
                        ELSE original_demo_name
                    END
                WHERE category_key IN (
                    'salate', 'stangerl', 'baguettes', 'calzone', 'pizza-mittel', 'pizza-partner',
                    'familien-pizza', 'mexikanische-pizza-mittel', 'mexikanische-pizza-partner',
                    'pasta', 'imbiss', 'burger', 'kebap', 'desserts', 'saucen', 'alkoholfreie-getranke'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "backup_schedule_configurations");

            migrationBuilder.DropColumn(
                name: "max_stock_level",
                table: "products");

            migrationBuilder.DropColumn(
                name: "session_idle_timeout_enabled",
                table: "system_settings");
        }
    }
}
