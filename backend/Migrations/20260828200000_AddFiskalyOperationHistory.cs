using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Tenant-scoped Fiskaly FA operation history (list, detail, retry).</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260828200000_AddFiskalyOperationHistory")]
public partial class AddFiskalyOperationHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fiskaly_operation_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                operation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                cash_register_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                receipt_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                receipt_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                user_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                request_payload_json = table.Column<string>(type: "jsonb", nullable: true),
                response_payload_json = table.Column<string>(type: "jsonb", nullable: true),
                error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true),
                retried_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fiskaly_operation_history", x => x.id);
                table.ForeignKey(
                    name: "FK_fiskaly_operation_history_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_fiskaly_operation_history_fiskaly_operation_history_retried_from_id",
                    column: x => x.retried_from_id,
                    principalTable: "fiskaly_operation_history",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_fiskaly_operation_history_tenant_id_created_at_utc",
            table: "fiskaly_operation_history",
            columns: new[] { "tenant_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_fiskaly_operation_history_tenant_id_operation_type_created_at_utc",
            table: "fiskaly_operation_history",
            columns: new[] { "tenant_id", "operation_type", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_fiskaly_operation_history_tenant_id_status_created_at_utc",
            table: "fiskaly_operation_history",
            columns: new[] { "tenant_id", "status", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_fiskaly_operation_history_receipt_number",
            table: "fiskaly_operation_history",
            column: "receipt_number");

        migrationBuilder.CreateIndex(
            name: "IX_fiskaly_operation_history_retried_from_id",
            table: "fiskaly_operation_history",
            column: "retried_from_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fiskaly_operation_history");
    }
}
