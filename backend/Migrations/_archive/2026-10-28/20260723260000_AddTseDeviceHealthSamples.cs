using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTseDeviceHealthSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tse_device_health_samples",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    health_score = table.Column<int>(type: "integer", nullable: false),
                    health_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_backup = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_device_health_samples", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_device_health_samples_TseDevices_device_id",
                        column: x => x.device_id,
                        principalTable: "TseDevices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tse_device_health_samples_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_tse_health_samples_device_checked",
                table: "tse_device_health_samples",
                columns: new[] { "device_id", "checked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_health_samples_tenant_checked",
                table: "tse_device_health_samples",
                columns: new[] { "tenant_id", "checked_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tse_device_health_samples");
        }
    }
}
