using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260914120000_AddCashRegisterOpenRequests")]
public partial class AddCashRegisterOpenRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cash_register_open_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                resolved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cash_register_open_requests", x => x.id);
                table.ForeignKey(
                    name: "FK_cash_register_open_requests_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_cash_register_open_requests_cash_registers_cash_register_id",
                    column: x => x.cash_register_id,
                    principalTable: "cash_registers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_cash_register_open_requests_tenant_id",
            table: "cash_register_open_requests",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_cash_register_open_requests_register_id",
            table: "cash_register_open_requests",
            column: "cash_register_id");

        migrationBuilder.CreateIndex(
            name: "ux_cash_register_open_requests_pending_register_user",
            table: "cash_register_open_requests",
            columns: new[] { "tenant_id", "cash_register_id", "requested_by_user_id" },
            unique: true,
            filter: "status = 'Pending'");

        migrationBuilder.CreateIndex(
            name: "idx_cash_register_open_requests_status_requested",
            table: "cash_register_open_requests",
            columns: new[] { "status", "requested_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "cash_register_open_requests");
    }
}
