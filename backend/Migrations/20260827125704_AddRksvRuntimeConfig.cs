using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRksvRuntimeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rksv_runtime_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tse_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finanz_online_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    show_demo_label = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_runtime_config", x => x.id);
                });

            // Singleton + allowed values. Row is seeded from appsettings on first access (not HasData).
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config ADD CONSTRAINT chk_rksv_runtime_config_singleton CHECK (id = 1);");
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config ADD CONSTRAINT chk_rksv_runtime_config_mode CHECK (mode IN ('Demo', 'Production'));");
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config ADD CONSTRAINT chk_rksv_runtime_config_tse_mode CHECK (tse_mode IN ('Simulation', 'Real'));");
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config ADD CONSTRAINT chk_rksv_runtime_config_fon_mode CHECK (finanz_online_mode IN ('Simulation', 'Real'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE rksv_runtime_config DROP CONSTRAINT IF EXISTS chk_rksv_runtime_config_fon_mode;");
            migrationBuilder.Sql("ALTER TABLE rksv_runtime_config DROP CONSTRAINT IF EXISTS chk_rksv_runtime_config_tse_mode;");
            migrationBuilder.Sql("ALTER TABLE rksv_runtime_config DROP CONSTRAINT IF EXISTS chk_rksv_runtime_config_mode;");
            migrationBuilder.Sql("ALTER TABLE rksv_runtime_config DROP CONSTRAINT IF EXISTS chk_rksv_runtime_config_singleton;");

            migrationBuilder.DropTable(
                name: "rksv_runtime_config");
        }
    }
}
