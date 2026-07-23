using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723250000_AddTseFailoverSupport")]
public partial class AddTseFailoverSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApiKey",
            table: "TseDevices",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApiSecret",
            table: "TseDevices",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CashRegisterId",
            table: "TseDevices",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Certificate",
            table: "TseDevices",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeviceId",
            table: "TseDevices",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiresAt",
            table: "TseDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiryWarningSentAt",
            table: "TseDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FailoverCount",
            table: "TseDevices",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "HealthMessage",
            table: "TseDevices",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "HealthScore",
            table: "TseDevices",
            type: "integer",
            nullable: false,
            defaultValue: 100);

        migrationBuilder.AddColumn<int>(
            name: "HealthStatus",
            table: "TseDevices",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "IsBackup",
            table: "TseDevices",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsFailoverActive",
            table: "TseDevices",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPrimary",
            table: "TseDevices",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "IssuedAt",
            table: "TseDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastFailoverAt",
            table: "TseDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastFailoverReason",
            table: "TseDevices",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastHealthCheck",
            table: "TseDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PrimaryDeviceId",
            table: "TseDevices",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Provider",
            table: "TseDevices",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            table: "TseDevices",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "tse_failover_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                primary_device_id = table.Column<Guid>(type: "uuid", nullable: false),
                backup_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                failover_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                trigger_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                previous_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                new_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                is_successful = table.Column<bool>(type: "boolean", nullable: false),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                performed_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_failover_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_failover_logs_AspNetUsers_performed_by",
                    column: x => x.performed_by,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tse_failover_logs_TseDevices_backup_device_id",
                    column: x => x.backup_device_id,
                    principalTable: "TseDevices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tse_failover_logs_TseDevices_primary_device_id",
                    column: x => x.primary_device_id,
                    principalTable: "TseDevices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tse_failover_logs_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TseDevices_CashRegisterId",
            table: "TseDevices",
            column: "CashRegisterId");

        migrationBuilder.CreateIndex(
            name: "IX_TseDevices_PrimaryDeviceId",
            table: "TseDevices",
            column: "PrimaryDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_TseDevices_Tenant_FailoverActive",
            table: "TseDevices",
            columns: new[] { "TenantId", "IsFailoverActive" });

        migrationBuilder.CreateIndex(
            name: "IX_TseDevices_Tenant_Primary_Active",
            table: "TseDevices",
            columns: new[] { "TenantId", "IsPrimary", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_TseDevices_TenantId",
            table: "TseDevices",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "idx_tse_failover_logs_backup_device",
            table: "tse_failover_logs",
            column: "backup_device_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_failover_logs_primary_device",
            table: "tse_failover_logs",
            column: "primary_device_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_failover_logs_tenant_id",
            table: "tse_failover_logs",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_failover_logs_tenant_started",
            table: "tse_failover_logs",
            columns: new[] { "tenant_id", "started_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_failover_logs_tenant_success",
            table: "tse_failover_logs",
            columns: new[] { "tenant_id", "is_successful" });

        migrationBuilder.CreateIndex(
            name: "IX_tse_failover_logs_performed_by",
            table: "tse_failover_logs",
            column: "performed_by");

        migrationBuilder.AddForeignKey(
            name: "FK_TseDevices_TseDevices_PrimaryDeviceId",
            table: "TseDevices",
            column: "PrimaryDeviceId",
            principalTable: "TseDevices",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TseDevices_cash_registers_CashRegisterId",
            table: "TseDevices",
            column: "CashRegisterId",
            principalTable: "cash_registers",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_TseDevices_tenants_TenantId",
            table: "TseDevices",
            column: "TenantId",
            principalTable: "tenants",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TseDevices_TseDevices_PrimaryDeviceId",
            table: "TseDevices");

        migrationBuilder.DropForeignKey(
            name: "FK_TseDevices_cash_registers_CashRegisterId",
            table: "TseDevices");

        migrationBuilder.DropForeignKey(
            name: "FK_TseDevices_tenants_TenantId",
            table: "TseDevices");

        migrationBuilder.DropTable(
            name: "tse_failover_logs");

        migrationBuilder.DropIndex(
            name: "IX_TseDevices_CashRegisterId",
            table: "TseDevices");

        migrationBuilder.DropIndex(
            name: "IX_TseDevices_PrimaryDeviceId",
            table: "TseDevices");

        migrationBuilder.DropIndex(
            name: "IX_TseDevices_Tenant_FailoverActive",
            table: "TseDevices");

        migrationBuilder.DropIndex(
            name: "IX_TseDevices_Tenant_Primary_Active",
            table: "TseDevices");

        migrationBuilder.DropIndex(
            name: "IX_TseDevices_TenantId",
            table: "TseDevices");

        migrationBuilder.DropColumn(name: "ApiKey", table: "TseDevices");
        migrationBuilder.DropColumn(name: "ApiSecret", table: "TseDevices");
        migrationBuilder.DropColumn(name: "CashRegisterId", table: "TseDevices");
        migrationBuilder.DropColumn(name: "Certificate", table: "TseDevices");
        migrationBuilder.DropColumn(name: "DeviceId", table: "TseDevices");
        migrationBuilder.DropColumn(name: "ExpiresAt", table: "TseDevices");
        migrationBuilder.DropColumn(name: "ExpiryWarningSentAt", table: "TseDevices");
        migrationBuilder.DropColumn(name: "FailoverCount", table: "TseDevices");
        migrationBuilder.DropColumn(name: "HealthMessage", table: "TseDevices");
        migrationBuilder.DropColumn(name: "HealthScore", table: "TseDevices");
        migrationBuilder.DropColumn(name: "HealthStatus", table: "TseDevices");
        migrationBuilder.DropColumn(name: "IsBackup", table: "TseDevices");
        migrationBuilder.DropColumn(name: "IsFailoverActive", table: "TseDevices");
        migrationBuilder.DropColumn(name: "IsPrimary", table: "TseDevices");
        migrationBuilder.DropColumn(name: "IssuedAt", table: "TseDevices");
        migrationBuilder.DropColumn(name: "LastFailoverAt", table: "TseDevices");
        migrationBuilder.DropColumn(name: "LastFailoverReason", table: "TseDevices");
        migrationBuilder.DropColumn(name: "LastHealthCheck", table: "TseDevices");
        migrationBuilder.DropColumn(name: "PrimaryDeviceId", table: "TseDevices");
        migrationBuilder.DropColumn(name: "Provider", table: "TseDevices");
        migrationBuilder.DropColumn(name: "TenantId", table: "TseDevices");
    }
}
