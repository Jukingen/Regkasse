using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionPlatformExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Best-effort rename: local DBs may already use *_Trgm or lowercase names.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."IX_invoices_InvoiceNumber"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_InvoiceNumber" RENAME TO ix_invoices_invoice_number_trgm;
                    ELSIF to_regclass('public."IX_invoices_InvoiceNumber_Trgm"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_InvoiceNumber_Trgm" RENAME TO ix_invoices_invoice_number_trgm;
                    END IF;

                    IF to_regclass('public."IX_invoices_CustomerName"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_CustomerName" RENAME TO ix_invoices_customer_name_trgm;
                    ELSIF to_regclass('public."IX_invoices_CustomerName_Trgm"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_CustomerName_Trgm" RENAME TO ix_invoices_customer_name_trgm;
                    END IF;

                    IF to_regclass('public."IX_invoices_CompanyName"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_CompanyName" RENAME TO ix_invoices_company_name_trgm;
                    ELSIF to_regclass('public."IX_invoices_CompanyName_Trgm"') IS NOT NULL THEN
                        ALTER INDEX "IX_invoices_CompanyName_Trgm" RENAME TO ix_invoices_company_name_trgm;
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "expired_processed_at",
                table: "user_permission_overrides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiring_notified_at",
                table: "user_permission_overrides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "valid_from",
                table: "user_permission_overrides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry_template_customizations",
                table: "tenants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry_template_id",
                table: "tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "download_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    download_url = table.Column<string>(type: "text", nullable: true),
                    downloaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "download_security_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    export_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_security_tickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_config_backup_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    auto_backup_before_changes = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_config_backup_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_config_backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    trigger = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_config_backups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requested_duration = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resulting_override_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_usage_daily",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_users = table.Column<int>(type: "integer", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_usage_daily", x => x.date);
                });

            migrationBuilder.CreateTable(
                name: "sensitive_export_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    export_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requester_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resource_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensitive_export_approvals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_package_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_package_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_permission_package_keys_permission_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "permission_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permission_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_permission_packages_permission_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "permission_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_expires_at",
                table: "user_permission_overrides",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_download_history_tenant_id",
                table: "download_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_permission_config_backups_created_at",
                table: "permission_config_backups",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_permission_package_keys_package_id",
                table: "permission_package_keys",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ux_permission_package_keys_package_permission",
                table: "permission_package_keys",
                columns: new[] { "package_id", "permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_permission_packages_slug",
                table: "permission_packages",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_permission_requests_requester",
                table: "permission_requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_permission_requests_status_requested",
                table: "permission_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_permission_requests_pending_requester_permission_tenant",
                table: "permission_requests",
                columns: new[] { "requester_user_id", "permission", "tenant_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_role_permission_packages_package_id",
                table: "role_permission_packages",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_permission_packages_role_package",
                table: "role_permission_packages",
                columns: new[] { "role_id", "package_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "download_history");

            migrationBuilder.DropTable(
                name: "download_security_tickets");

            migrationBuilder.DropTable(
                name: "permission_config_backup_settings");

            migrationBuilder.DropTable(
                name: "permission_config_backups");

            migrationBuilder.DropTable(
                name: "permission_package_keys");

            migrationBuilder.DropTable(
                name: "permission_requests");

            migrationBuilder.DropTable(
                name: "permission_usage_daily");

            migrationBuilder.DropTable(
                name: "role_permission_packages");

            migrationBuilder.DropTable(
                name: "sensitive_export_approvals");

            migrationBuilder.DropTable(
                name: "permission_packages");

            migrationBuilder.DropIndex(
                name: "IX_user_permission_overrides_expires_at",
                table: "user_permission_overrides");

            migrationBuilder.DropColumn(
                name: "expired_processed_at",
                table: "user_permission_overrides");

            migrationBuilder.DropColumn(
                name: "expiring_notified_at",
                table: "user_permission_overrides");

            migrationBuilder.DropColumn(
                name: "valid_from",
                table: "user_permission_overrides");

            migrationBuilder.DropColumn(
                name: "industry_template_customizations",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "industry_template_id",
                table: "tenants");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.ix_invoices_invoice_number_trgm') IS NOT NULL THEN
                        ALTER INDEX ix_invoices_invoice_number_trgm RENAME TO "IX_invoices_InvoiceNumber";
                    END IF;
                    IF to_regclass('public.ix_invoices_customer_name_trgm') IS NOT NULL THEN
                        ALTER INDEX ix_invoices_customer_name_trgm RENAME TO "IX_invoices_CustomerName";
                    END IF;
                    IF to_regclass('public.ix_invoices_company_name_trgm') IS NOT NULL THEN
                        ALTER INDEX ix_invoices_company_name_trgm RENAME TO "IX_invoices_CompanyName";
                    END IF;
                END $$;
                """);
        }
    }
}
