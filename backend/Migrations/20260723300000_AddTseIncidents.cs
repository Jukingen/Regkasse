using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723300000_AddTseIncidents")]
public partial class AddTseIncidents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_incidents",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_incidents", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_incidents_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tse_incidents_devices",
                    column: x => x.device_id,
                    principalTable: "TseDevices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "tse_incident_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_incident_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_incident_logs_incidents",
                    column: x => x.incident_id,
                    principalTable: "tse_incidents",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tse_incident_actions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                performed_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_completed = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_incident_actions", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_incident_actions_incidents",
                    column: x => x.incident_id,
                    principalTable: "tse_incidents",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_incidents_tenant_detected",
            table: "tse_incidents",
            columns: new[] { "tenant_id", "detected_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_incidents_status",
            table: "tse_incidents",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_tse_incidents_severity",
            table: "tse_incidents",
            column: "severity");

        migrationBuilder.CreateIndex(
            name: "idx_tse_incidents_device_id",
            table: "tse_incidents",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_incident_logs_incident_id",
            table: "tse_incident_logs",
            column: "incident_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_incident_actions_incident_id",
            table: "tse_incident_actions",
            column: "incident_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_incident_logs");
        migrationBuilder.DropTable(name: "tse_incident_actions");
        migrationBuilder.DropTable(name: "tse_incidents");
    }
}
