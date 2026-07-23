using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723220000_AddOperationLogs")]
public partial class AddOperationLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "operation_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                operation_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                before_state = table.Column<string>(type: "jsonb", nullable: true),
                after_state = table.Column<string>(type: "jsonb", nullable: true),
                is_undone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                undone_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                undone_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operation_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_operation_logs_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_operation_logs_tenant_id",
            table: "operation_logs",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_operation_logs_tenant_created",
            table: "operation_logs",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_operation_logs_tenant_type",
            table: "operation_logs",
            columns: new[] { "tenant_id", "operation_type" });

        migrationBuilder.CreateIndex(
            name: "idx_operation_logs_tenant_undone",
            table: "operation_logs",
            columns: new[] { "tenant_id", "is_undone" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "operation_logs");
    }
}
