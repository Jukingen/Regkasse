using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723330000_AddTseApiGateway")]
public partial class AddTseApiGateway : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_gateway_configs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                health_check_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                timeout_ms = table.Column<int>(type: "integer", nullable: false),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_gateway_configs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tse_gateway_endpoints",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                config_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                endpoint_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                weight = table.Column<int>(type: "integer", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_gateway_endpoints", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_gateway_endpoints_config",
                    column: x => x.config_id,
                    principalTable: "tse_gateway_configs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_gateway_endpoints_config",
            table: "tse_gateway_endpoints",
            column: "config_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_gateway_endpoints_config_provider",
            table: "tse_gateway_endpoints",
            columns: new[] { "config_id", "provider" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_gateway_endpoints");
        migrationBuilder.DropTable(name: "tse_gateway_configs");
    }
}
