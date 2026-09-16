using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260914160000_AddMonatsbelegAutoCreate")]
public partial class AddMonatsbelegAutoCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "auto_monatsbeleg_enabled",
            table: "company_settings",
            type: "boolean",
            nullable: false,
            defaultValue: true,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: false);

        migrationBuilder.Sql("UPDATE company_settings SET auto_monatsbeleg_enabled = TRUE;");

        migrationBuilder.AddColumn<int>(
            name: "monatsbeleg_retry_count",
            table: "company_settings",
            type: "integer",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.CreateTable(
            name: "monatsbeleg_auto_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                year = table.Column<int>(type: "integer", nullable: false),
                month = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                last_attempt_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_retry_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_monatsbeleg_auto_runs", x => x.id);
                table.ForeignKey(
                    name: "FK_monatsbeleg_auto_runs_cash_registers_cash_register_id",
                    column: x => x.cash_register_id,
                    principalTable: "cash_registers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_monatsbeleg_auto_runs_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_monatsbeleg_auto_runs_tenant_id",
            table: "monatsbeleg_auto_runs",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_monatsbeleg_auto_runs_register_id",
            table: "monatsbeleg_auto_runs",
            column: "cash_register_id");

        migrationBuilder.CreateIndex(
            name: "ux_monatsbeleg_auto_runs_register_period",
            table: "monatsbeleg_auto_runs",
            columns: new[] { "tenant_id", "cash_register_id", "year", "month" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "monatsbeleg_auto_runs");

        migrationBuilder.DropColumn(
            name: "monatsbeleg_retry_count",
            table: "company_settings");

        migrationBuilder.AlterColumn<bool>(
            name: "auto_monatsbeleg_enabled",
            table: "company_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: true);
    }
}
