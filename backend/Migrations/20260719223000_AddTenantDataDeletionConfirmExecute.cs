using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260719223000_AddTenantDataDeletionConfirmExecute")]
public partial class AddTenantDataDeletionConfirmExecute : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "confirmed_at_utc",
            table: "tenant_data_deletion_requests",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "confirmed_by_user_id",
            table: "tenant_data_deletion_requests",
            type: "character varying(450)",
            maxLength: 450,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "executed_via",
            table: "tenant_data_deletion_requests",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "confirmed_at_utc", table: "tenant_data_deletion_requests");
        migrationBuilder.DropColumn(name: "confirmed_by_user_id", table: "tenant_data_deletion_requests");
        migrationBuilder.DropColumn(name: "executed_via", table: "tenant_data_deletion_requests");
    }
}
