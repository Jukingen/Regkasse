using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Registrierkasse.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigurationAndInvoiceFields2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "customer_details",
                table: "invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_date",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_type",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_electronic",
                table: "invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_void",
                table: "invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "original_invoice_id",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "payment_details",
                table: "invoices",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "qr_code",
                table: "invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "related_invoice_ids",
                table: "invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "tax_summary",
                table: "invoices",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "tse_certificate",
                table: "invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "tse_process_data",
                table: "invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tse_process_type",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tse_serial_number",
                table: "invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "tse_signature_counter",
                table: "invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "tse_time",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OfflineSettings_Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    OfflineSettings_SyncInterval = table.Column<int>(type: "integer", nullable: false),
                    OfflineSettings_MaxOfflineDays = table.Column<int>(type: "integer", nullable: false),
                    OfflineSettings_AutoSync = table.Column<bool>(type: "boolean", nullable: false),
                    TseSettings_Required = table.Column<bool>(type: "boolean", nullable: false),
                    TseSettings_OfflineAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    TseSettings_MaxOfflineTransactions = table.Column<int>(type: "integer", nullable: false),
                    PrinterSettings_Required = table.Column<bool>(type: "boolean", nullable: false),
                    PrinterSettings_OfflineQueue = table.Column<bool>(type: "boolean", nullable: false),
                    PrinterSettings_MaxQueueSize = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_info = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_sessions_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_original_invoice_id",
                table: "invoices",
                column: "original_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_invoices_original_invoice_id",
                table: "invoices",
                column: "original_invoice_id",
                principalTable: "invoices",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_invoices_original_invoice_id",
                table: "invoices");

            migrationBuilder.DropTable(
                name: "SystemConfigurations");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_invoices_original_invoice_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_details",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_type",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "is_electronic",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "is_void",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "original_invoice_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "payment_details",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "qr_code",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "related_invoice_ids",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tax_summary",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_certificate",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_process_data",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_process_type",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_serial_number",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_signature_counter",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tse_time",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "void_reason",
                table: "invoices");
        }
    }
}
