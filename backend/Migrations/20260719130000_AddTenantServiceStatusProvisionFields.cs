using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260719130000_AddTenantServiceStatusProvisionFields")]
public partial class AddTenantServiceStatusProvisionFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "tenant_service_statuses",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "none");

        migrationBuilder.AddColumn<string>(
            name: "url",
            table: "tenant_service_statuses",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "template_id",
            table: "tenant_service_statuses",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "customization",
            table: "tenant_service_statuses",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "requested_at",
            table: "tenant_service_statuses",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "artifact_created_at",
            table: "tenant_service_statuses",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "published_at",
            table: "tenant_service_statuses",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_tenant_service_statuses_status",
            table: "tenant_service_statuses",
            column: "status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_tenant_service_statuses_status",
            table: "tenant_service_statuses");

        migrationBuilder.DropColumn(name: "status", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "url", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "template_id", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "customization", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "requested_at", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "artifact_created_at", table: "tenant_service_statuses");
        migrationBuilder.DropColumn(name: "published_at", table: "tenant_service_statuses");
    }
}
