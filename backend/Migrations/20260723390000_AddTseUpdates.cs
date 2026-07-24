using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723390000_AddTseUpdates")]
public partial class AddTseUpdates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_update_states",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                update_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                current_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_update_states", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_update_states_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_update_states_tenant_type",
            table: "tse_update_states",
            columns: new[] { "tenant_id", "update_type" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "tse_update_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                update_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                risk_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                from_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                to_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                zero_downtime = table.Column<bool>(type: "boolean", nullable: false),
                devices_touched = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                applied_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_update_history", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_update_history_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_update_history_tenant_started",
            table: "tse_update_history",
            columns: new[] { "tenant_id", "started_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_update_history");
        migrationBuilder.DropTable(name: "tse_update_states");
    }
}
