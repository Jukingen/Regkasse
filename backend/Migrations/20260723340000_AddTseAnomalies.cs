using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723340000_AddTseAnomalies")]
public partial class AddTseAnomalies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_anomalies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                metric_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                current_value = table.Column<double>(type: "double precision", nullable: false),
                expected_value = table.Column<double>(type: "double precision", nullable: false),
                deviation_percent = table.Column<double>(type: "double precision", nullable: false),
                severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                suggested_action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_anomalies", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_anomalies_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tse_anomalies_tse_devices",
                    column: x => x.device_id,
                    principalTable: "TseDevices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_anomalies_tenant_detected",
            table: "tse_anomalies",
            columns: new[] { "tenant_id", "detected_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_anomalies_tenant_open_severity",
            table: "tse_anomalies",
            columns: new[] { "tenant_id", "is_resolved", "severity" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_anomalies_dedup",
            table: "tse_anomalies",
            columns: new[] { "tenant_id", "metric_name", "device_id", "is_resolved" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_anomalies");
    }
}
