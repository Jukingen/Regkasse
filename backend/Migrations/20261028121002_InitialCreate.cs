using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "activated_licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    machine_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    features_json = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activated_licenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    dedup_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    path_hint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    employee_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tax_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    account_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    login_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    deactivation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    must_change_password_on_next_login = table.Column<bool>(type: "boolean", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipients_json = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_report_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_retention_policy_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    hot_retention_days = table.Column<int>(type: "integer", nullable: false),
                    warm_retention_days = table.Column<int>(type: "integer", nullable: false),
                    cold_retention_years = table.Column<int>(type: "integer", nullable: false),
                    cold_storage_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    legal_retention_enforced = table.Column<bool>(type: "boolean", nullable: false),
                    cloud_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_retention_policy_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_runtime_execution_preferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runtime_execution_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backup_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "demo_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    industry = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exported_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    signature_count = table.Column<int>(type: "integer", nullable: false),
                    group_count = table.Column<int>(type: "integer", nullable: false),
                    legacy_jws_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    download_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    download_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    downloaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    download_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    include_special_receipts = table.Column<bool>(type: "boolean", nullable: false),
                    include_daily_closings = table.Column<bool>(type: "boolean", nullable: false),
                    is_simulated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    simulation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    validation_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    validation_report_json = table.Column<string>(type: "jsonb", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archive_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    archive_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    retention_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purge_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    day_of_month = table.Column<int>(type: "integer", nullable: false),
                    time_of_day = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    recipient_emails = table.Column<string>(type: "text", nullable: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deployment_compliance_signoffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    checklist_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    signed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    signed_by_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    signed_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    signed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_compliance_signoffs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deployment_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    git_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    image_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tenant_ids_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    run_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smoke_passed = table.Column<bool>(type: "boolean", nullable: true),
                    smoke_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    previous_image_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "development_mode_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_license = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_ntp_check = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_tse_check = table.Column<bool>(type: "boolean", nullable: false),
                    simulate_offline = table.Column<bool>(type: "boolean", nullable: false),
                    force_online = table.Column<bool>(type: "boolean", nullable: false),
                    valid_days = table.Column<int>(type: "integer", nullable: false),
                    features = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_development_mode_settings", x => x.id);
                });

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
                name: "export_email_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    delivery_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    download_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    download_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    artifact_relative_path = table.Column<string>(type: "text", nullable: true),
                    scheduled_for_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_email_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finanz_online_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BranchId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AggregateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BusinessKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TransmissionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalReferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProtocolCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProtocolPayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProtocolSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessingToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finanz_online_outbox_messages", x => x.Id);
                });

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
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "open"),
                    error_reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_reviewed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiskaly_operation_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiskaly_operation_history_fiskaly_operation_history_retried~",
                        column: x => x.retried_from_id,
                        principalTable: "fiskaly_operation_history",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gateway_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    applied = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStock = table.Column<int>(type: "integer", nullable: false),
                    MinStockLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxStockLevel = table.Column<int>(type: "integer", nullable: true),
                    ReorderPoint = table.Column<int>(type: "integer", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LastRestocked = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    last_sequence = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_sequences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issued_licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expiry_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    require_fingerprint = table.Column<bool>(type: "boolean", nullable: false),
                    machine_hash_hex = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    signed_jwt = table.Column<string>(type: "text", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    superseded_by_license_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transferred_to_license_id = table.Column<Guid>(type: "uuid", nullable: true),
                    features_json = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_licenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_licenses_issued_licenses_superseded_by_license_id",
                        column: x => x.superseded_by_license_id,
                        principalTable: "issued_licenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_licenses_issued_licenses_transferred_to_license_id",
                        column: x => x.transferred_to_license_id,
                        principalTable: "issued_licenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateTime>(type: "date", nullable: false),
                    to_date = table.Column<DateTime>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_holds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_activation_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    machine_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    activation_status = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deactivated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_activation_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_key_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    new_license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    license_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_table = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_key_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    scheduled_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    is_force_display = table.Column<bool>(type: "boolean", nullable: false),
                    force_display_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    affected_systems = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_7d_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_3d_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_24h_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_1h_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ntp_admin_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    auto_sync_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_allowed_offset_seconds = table.Column<int>(type: "integer", nullable: false),
                    critical_offset_seconds = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntp_admin_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "offline_intent_coverage_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    has_device_id = table.Column<bool>(type: "boolean", nullable: false),
                    has_client_sequence = table.Column<bool>(type: "boolean", nullable: false),
                    replay_batch_correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_intent_coverage_samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "operational_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    report_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipients_json = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_report_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_reversal_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approval_token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    approval_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_reversal_approvals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMetrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessingTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ServerLoad = table.Column<int>(type: "integer", nullable: false),
                    MemoryUsage = table.Column<long>(type: "bigint", nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: true),
                    PaymentGateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsTseRequired = table.Column<bool>(type: "boolean", nullable: true),
                    IsTseAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    TseResponseTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    NetworkLatency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DatabaseQueryCount = table.Column<int>(type: "integer", nullable: true),
                    DatabaseQueryTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMetrics", x => x.id);
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
                name: "receipt_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_date = table.Column<DateTime>(type: "date", nullable: false),
                    next_sequence = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_sequences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    HeaderTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FooterTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ItemTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaxTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaymentTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompanyTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CustomFields = table.Column<string>(type: "jsonb", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_pdfs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by_user_id = table.Column<string>(type: "text", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_pdfs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rksv_ausfall_episodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    episode_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    begruendung = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    beginn_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ende_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    certificate_serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    kassen_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_ausfall_episode_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operator_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    approved_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_ausfall_episodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rksv_cold_archive_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cutoff_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archive_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    payment_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_cold_archive_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rksv_runtime_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tse_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finanz_online_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    show_demo_label = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_tse_in_development = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_runtime_config", x => x.id);
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
                name: "suspicious_transaction_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    suggested_action = table.Column<string>(type: "text", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: true),
                    dedup_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    detected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suspicious_transaction_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_time_sync_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sync_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    system_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ntp_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    offset_seconds = table.Column<double>(type: "double precision", nullable: false),
                    ntp_server_used = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_time_sync_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_configs",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_configs", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "tse_blockchain_ledger_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_block_number = table.Column<long>(type: "bigint", nullable: false),
                    tip_block_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    network_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_connected = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_transactions = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_blockchain_ledger_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tse_gateway_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    health_check_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    timeout_ms = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_gateway_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tse_health_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    ReasonSafe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_health_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tse_knowledge_articles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_faq = table.Column<bool>(type: "boolean", nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    rating_sum = table.Column<int>(type: "integer", nullable: false),
                    rating_count = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_knowledge_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tse_resource_pools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    pool_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    total_capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_resource_pools", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tse_training_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    module_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_started = table.Column<bool>(type: "boolean", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_training_progress", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vertical_profile_overrides",
                columns: table => new
                {
                    profile_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pos_features_json = table.Column<string>(type: "jsonb", nullable: false),
                    required_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    optional_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    pos_layout = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vertical_profile_overrides", x => x.profile_id);
                });

            migrationBuilder.CreateTable(
                name: "vertical_profiles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pos_features = table.Column<string>(type: "jsonb", nullable: false),
                    required_fields = table.Column<string>(type: "jsonb", nullable: false),
                    optional_fields = table.Column<string>(type: "jsonb", nullable: false),
                    pos_layout = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vertical_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_event_reads",
                columns: table => new
                {
                    activity_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_event_reads", x => new { x.activity_event_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_activity_event_reads_activity_events_activity_event_id",
                        column: x => x.activity_event_id,
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    widgets = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dashboard_preferences_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiring_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    theme_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    density_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_page = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    time_format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    reduced_animations = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_preferences_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_username_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    old_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    new_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_username_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_username_history_AspNetUsers_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_username_history_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeFormat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CashRegisterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultTaxRate = table.Column<int>(type: "integer", nullable: false),
                    EnableDiscounts = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCoupons = table.Column<bool>(type: "boolean", nullable: false),
                    AutoPrintReceipts = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptHeader = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptFooter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TseDeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FinanzOnlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FinanzOnlineUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SessionTimeout = table.Column<int>(type: "integer", nullable: false),
                    RequirePinForRefunds = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercentage = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompactMode = table.Column<bool>(type: "boolean", nullable: false),
                    ShowProductImages = table.Column<bool>(type: "boolean", nullable: false),
                    EnableNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    LowStockAlert = table.Column<bool>(type: "boolean", nullable: false),
                    DailyReportEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DefaultPaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultTableNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DefaultWaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_compliance_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exported_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_compliance_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_dep_export_compliance_periods_dep_export_history_history_id",
                        column: x => x.history_id,
                        principalTable: "dep_export_history",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inventory_transactions_inventory_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_notification_acknowledgments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    is_dismissed = table.Column<bool>(type: "boolean", nullable: false),
                    dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_notification_acknowledgments", x => x.id);
                    table.ForeignKey(
                        name: "FK_maintenance_notification_acknowledgments_maintenance_notifi~",
                        column: x => x.notification_id,
                        principalTable: "maintenance_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "generated_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TemplateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GeneratedContent = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_receipts_receipt_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "receipt_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rksv_cold_archive_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    archive_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_cold_archive_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_rksv_cold_archive_items_rksv_cold_archive_runs_archive_run_~",
                        column: x => x.archive_run_id,
                        principalTable: "rksv_cold_archive_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_gateway_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    endpoint_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_gateway_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_gateway_endpoints_tse_gateway_configs_config_id",
                        column: x => x.config_id,
                        principalTable: "tse_gateway_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_knowledge_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_knowledge_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_knowledge_feedback_tse_knowledge_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "tse_knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_resource_pool_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rule_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_resource_pool_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_resource_pool_rules_tse_resource_pools_pool_id",
                        column: x => x.pool_id,
                        principalTable: "tse_resource_pools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "addon_group_products",
                columns: table => new
                {
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addon_group_products", x => new { x.modifier_group_id, x.product_id });
                });

            migrationBuilder.CreateTable(
                name: "admin_user_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    page_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    submitted_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewer_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_user_feedback", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    old_values = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_values = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    request_data = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    response_data = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    processing_time_ms = table.Column<double>(type: "double precision", nullable: true),
                    error_details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tse_signature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    action_type = table.Column<int>(type: "integer", nullable: true),
                    impersonated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    impersonated_tenant = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    client_app = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_activity_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    device_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    access_jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_auth_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "auth_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backup_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<int>(type: "integer", nullable: false),
                    storage_descriptor = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: true),
                    content_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    lifecycle_state = table.Column<int>(type: "integer", nullable: false),
                    storage_tier = table.Column<int>(type: "integer", nullable: false),
                    external_redacted_locator = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    cloud_locator = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    cloud_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    compressed_byte_size = table.Column<long>(type: "bigint", nullable: true),
                    deduplicated_from_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    moved_to_cold_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    immutable_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    trigger_source = table.Column<int>(type: "integer", nullable: false),
                    adapter_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    strategy = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_from_ip = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    download_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_heartbeat_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stale_recovered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stale_recovery_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    config_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    automatic_retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_recorded_terminal_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    automatic_retry_pending_classified_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    automatic_retry_last_scheduled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    legal_hold = table.Column<bool>(type: "boolean", nullable: false),
                    legal_hold_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    legal_hold_reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    legal_hold_set_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    legal_hold_set_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verifier_source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    completeness_flag = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_verifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_backup_verifications_backup_runs_backup_run_id",
                        column: x => x.backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restore_verification_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    trigger_source = table.Column<int>(type: "integer", nullable: false),
                    source_backup_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_backup_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dump_relative_descriptor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    pg_restore_list_passed = table.Column<bool>(type: "boolean", nullable: true),
                    pg_restore_list_exit_code = table.Column<int>(type: "integer", nullable: true),
                    pg_restore_list_line_count = table.Column<int>(type: "integer", nullable: true),
                    restore_attempt_executed = table.Column<bool>(type: "boolean", nullable: false),
                    restore_attempt_passed = table.Column<bool>(type: "boolean", nullable: true),
                    restore_attempt_exit_code = table.Column<int>(type: "integer", nullable: true),
                    restore_attempt_skip_reason = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    restore_target_db_redacted = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    fiscal_sql_skipped = table.Column<bool>(type: "boolean", nullable: false),
                    fiscal_sql_skip_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiscal_sql_passed = table.Column<bool>(type: "boolean", nullable: true),
                    fiscal_sql_fail_count = table.Column<int>(type: "integer", nullable: true),
                    fiscal_sql_warn_count = table.Column<int>(type: "integer", nullable: true),
                    integrity_scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    integrity_checks_passed = table.Column<bool>(type: "boolean", nullable: true),
                    post_restore_continuity_checks_executed = table.Column<bool>(type: "boolean", nullable: false),
                    post_restore_continuity_checks_passed = table.Column<bool>(type: "boolean", nullable: true),
                    post_restore_l4_continuity_proof_state = table.Column<int>(type: "integer", nullable: true),
                    fiscal_continuity_layer_passed = table.Column<bool>(type: "boolean", nullable: true),
                    restored_database_application_smoke_executed = table.Column<bool>(type: "boolean", nullable: false),
                    restored_database_application_smoke_result_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    restored_database_application_smoke_passed = table.Column<bool>(type: "boolean", nullable: true),
                    application_smoke_probe_executed = table.Column<bool>(type: "boolean", nullable: false),
                    application_smoke_probe_passed = table.Column<bool>(type: "boolean", nullable: true),
                    external_dependency_proof_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    external_dependency_l6_overall_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    external_dependency_l6_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    restore_drill_reached_stage = table.Column<short>(type: "smallint", nullable: true),
                    failure_category = table.Column<short>(type: "smallint", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    evidence_json = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_heartbeat_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stale_recovered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stale_recovery_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    config_snapshot_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_verification_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_restore_verification_runs_backup_artifacts_source_backup_ar~",
                        column: x => x.source_backup_artifact_id,
                        principalTable: "backup_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_restore_verification_runs_backup_runs_source_backup_run_id",
                        column: x => x.source_backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "manual_restore_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_database_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    validation_only = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approval_token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approval_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_by_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    approved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    result = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    restore_verification_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_restore_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_manual_restore_requests_backup_runs_backup_run_id",
                        column: x => x.backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_restore_requests_restore_verification_runs_restore_v~",
                        column: x => x.restore_verification_run_id,
                        principalTable: "restore_verification_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "backup_schedule_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_schedule_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "benefit_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    benefit_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "benefit_daily_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    benefit_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage_date = table.Column<DateTime>(type: "date", nullable: false),
                    quantity_used = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_daily_usage", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "benefit_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    benefit_kind = table.Column<int>(type: "integer", nullable: false),
                    percentage_value = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    allowance_quantity = table.Column<int>(type: "integer", nullable: true),
                    allowance_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    allowance_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    buy_x_quantity = table.Column<int>(type: "integer", nullable: true),
                    get_y_quantity = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_backup_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    backup_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    backup_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "success"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    triggered_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retention_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_backup_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cart_item_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_item_modifiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    applied_pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    CartId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: true),
                    WaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carts", x => x.CartId);
                    table.UniqueConstraint("AK_carts_id", x => x.id);
                    table.ForeignKey(
                        name: "FK_carts_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_carts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                });

            migrationBuilder.CreateTable(
                name: "cash_register_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_default_on_pos_entry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    auto_open_sole_closed_register = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    auto_open_assigned_closed_register = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_auto_open_opening_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_register_settings", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "cash_register_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_register_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_register_transactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_registers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LastBalanceUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    assigned_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_monatsbeleg_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    last_jahresbeleg_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    startbeleg_created_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    last_server_time_offset_seconds = table.Column<double>(type: "double precision", nullable: true),
                    last_server_time_drift_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    decommissioned_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    decommission_reason = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_default_for_tenant = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_registers", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                        column: x => x.CurrentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cash_registers_AspNetUsers_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FinanzOnlineErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanzOnlineErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanzOnlineErrors_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "jahresbericht_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViennaYearStart = table.Column<DateTime>(type: "date", nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrectionKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionOfReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededByReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportVersion = table.Column<int>(type: "integer", nullable: false),
                    ReportRevisionReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RebuildCause = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrectionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmissionImpact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LastFinanzOnlineOutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSubmissionStatusCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastSubmissionError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotGrossSalesAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpstreamReviewRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpstreamReviewReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jahresbericht_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jahresbericht_reports_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "monatsbericht_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViennaMonthStart = table.Column<DateTime>(type: "date", nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrectionKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionOfReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededByReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportVersion = table.Column<int>(type: "integer", nullable: false),
                    ReportRevisionReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RebuildCause = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrectionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmissionImpact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LastFinanzOnlineOutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSubmissionStatusCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastSubmissionError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotGrossSalesAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpstreamReviewRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpstreamReviewReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monatsbericht_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monatsbericht_reports_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "periodenbericht_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodPreset = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PeriodStartLocalDate = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEndLocalDate = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashierId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    PaymentMethodFilter = table.Column<int>(type: "integer", nullable: true),
                    ActiveOnly = table.Column<bool>(type: "boolean", nullable: false),
                    QueryParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    QueryParametersHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentRowCount = table.Column<int>(type: "integer", nullable: false),
                    GrossTotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxTotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundRowCount = table.Column<int>(type: "integer", nullable: false),
                    RefundAmountTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ExportProfileKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodenbericht_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_periodenbericht_runs_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days_of_week_mask = table.Column<int>(type: "integer", nullable: false),
                    time_window_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    time_start_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_end_minutes = table.Column<int>(type: "integer", nullable: false),
                    target_scope = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<int>(type: "integer", nullable: false),
                    action_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_rules_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tagesbericht_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViennaBusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OperatorUserIdScope = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrectionKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionOfReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededByReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportVersion = table.Column<int>(type: "integer", nullable: false),
                    ReportRevisionReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RebuildCause = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrectionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmissionImpact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LinkedDailyClosingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LastFinanzOnlineOutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSubmissionStatusCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastSubmissionError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotGrossSalesAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tagesbericht_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tagesbericht_reports_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cashier_favorites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashier_favorites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cashier_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    cashier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    end_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_sales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_cash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_card = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    daily_closing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_count = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    is_auto_opened = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_auto_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashier_shifts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cashier_shifts_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_demo_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    fiscal_category = table.Column<int>(type: "integer", nullable: false),
                    is_system_category = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("AK_categories_id_tenant_id", x => new { x.id, x.tenant_id });
                });

            migrationBuilder.CreateTable(
                name: "ch_receipt_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ch_receipt_sequences", x => x.id);
                    table.ForeignKey(
                        name: "FK_ch_receipt_sequences_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "communication_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyWebsite = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompanyRegistrationNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyVatNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyLogo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thank_you_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessHours = table.Column<string>(type: "jsonb", nullable: false),
                    working_hours = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    auto_tagesabschluss = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankRoutingNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankSwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "AT"),
                    vertical_profile_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    billing_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    vat_regime = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "AT_RKSV_STANDARD"),
                    tax_exempt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    TaxCalculationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceNumbering = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceiptNumbering = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultPaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FinanzOnlineApiUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FinanzOnlineUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FinanzOnlinePassword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FinanzOnlineTelematikId = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    FinanzOnlineHerstellerId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    FinanzOnlineAutoSubmit = table.Column<bool>(type: "boolean", nullable: false),
                    FinanzOnlineSubmitInterval = table.Column<int>(type: "integer", nullable: false),
                    FinanzOnlineRetryAttempts = table.Column<int>(type: "integer", nullable: false),
                    FinanzOnlineEnableValidation = table.Column<bool>(type: "boolean", nullable: false),
                    FinanzOnlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFinanzOnlineSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PendingInvoices = table.Column<int>(type: "integer", nullable: true),
                    DefaultTseDeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TseAutoConnect = table.Column<bool>(type: "boolean", nullable: false),
                    TseConnectionTimeout = table.Column<int>(type: "integer", nullable: false),
                    use_december_monatsbeleg_as_jahresbeleg = table.Column<bool>(type: "boolean", nullable: false),
                    monatsbeleg_blocking_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Strict"),
                    auto_monatsbeleg_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    monatsbeleg_retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    preorder_pickup_deadline_weeks = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    preorder_cancellation_policy_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    de_tss_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    de_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    taxi_tariff_per_km = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    kitchen_order_auto_clear_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    kitchen_order_sound = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_settings_vertical_profiles_vertical_profile_id",
                        column: x => x.vertical_profile_id,
                        principalTable: "vertical_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_category = table.Column<int>(type: "integer", nullable: false),
                    loyalty_points = table.Column<int>(type: "integer", nullable: false),
                    total_spent = table.Column<decimal>(type: "numeric", nullable: false),
                    visit_count = table.Column<int>(type: "integer", nullable: false),
                    last_visit = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    clinical_data = table.Column<string>(type: "jsonb", nullable: true),
                    address_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_vip = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    discount_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    preferred_payment_method = table.Column<int>(type: "integer", nullable: false),
                    application_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_customers_AspNetUsers_application_user_id",
                        column: x => x.application_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: true),
                    WaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location_data = table.Column<string>(type: "jsonb", nullable: true),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_preorder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    preorder_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    preorder_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    preorder_paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    preorder_remaining_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    preorder_pickup_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    preorder_pickup_weeks = table.Column<int>(type: "integer", nullable: false),
                    preorder_ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    preorder_collected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    preorder_customer_notes = table.Column<string>(type: "text", nullable: true),
                    source_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_preorder_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.UniqueConstraint("AK_orders_OrderId", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_orders_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_orders_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "table_orders",
                columns: table => new
                {
                    TableOrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    WaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OrderStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CartId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StatusHistory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_orders", x => x.TableOrderId);
                    table.ForeignKey(
                        name: "FK_table_orders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_table_orders_carts_CartId",
                        column: x => x.CartId,
                        principalTable: "carts",
                        principalColumn: "CartId");
                    table.ForeignKey(
                        name: "FK_table_orders_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SpecialNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProductDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProductCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "table_order_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TableOrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TaxType = table.Column<int>(type: "integer", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreparedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_table_order_items_table_orders_TableOrderId",
                        column: x => x.TableOrderId,
                        principalTable: "table_orders",
                        principalColumn: "TableOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "table_order_item_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_order_item_modifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_table_order_item_modifiers_table_order_items_table_order_it~",
                        column: x => x.table_order_item_id,
                        principalTable: "table_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    cashier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    shift_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ClosingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_backdated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    late_creation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    cash_count_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cash_count = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    cash_difference = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    open_orders_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    open_orders_forced = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClosingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    day_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "normal"),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    TseSignature = table.Column<string>(type: "text", nullable: true),
                    tse_signature_timestamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousSignature = table.Column<string>(type: "text", nullable: true),
                    SignatureChainLength = table.Column<int>(type: "integer", nullable: false),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SignatureFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JwsHeader = table.Column<string>(type: "text", nullable: true),
                    JwsPayload = table.Column<string>(type: "text", nullable: true),
                    JwsSignature = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinanzOnlineStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FinanzOnlineError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FinanzOnlineReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiskaly_receipt_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    fiskaly_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fiskaly_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fiskaly_submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyClosings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyClosings_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "de_receipt_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_de_receipt_sequences", x => x.id);
                    table.ForeignKey(
                        name: "FK_de_receipt_sequences_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "de_tse_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_details_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tss_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signature = table.Column<string>(type: "text", nullable: false),
                    signature_algorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    signed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    certificate_serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_de_tse_signatures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    export_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    export_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    action_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_compliance_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    grade = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    factors_json = table.Column<string>(type: "jsonb", nullable: false),
                    critical_issues_json = table.Column<string>(type: "jsonb", nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_compliance_scores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deployment_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    previous_version = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    git_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    run_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    smoke_passed = table.Column<bool>(type: "boolean", nullable: true),
                    deployed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    soak_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployment_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "digital_service_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resolved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_service_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "digital_service_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_billing_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    cancelled_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_service_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "einvoice_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    provider_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    provider_attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eu_receipt_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eu_receipt_sequences", x => x.id);
                    table.ForeignKey(
                        name: "FK_eu_receipt_sequences_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanzOnlineSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseStatusCode = table.Column<string>(type: "text", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "jsonb", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanzOnlineSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gateway_payment_intents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gateway = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gateway_payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    card_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    card_brand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    refunded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    redirect_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    return_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    method_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_webhook_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    capture_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "automatic"),
                    cart_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_payment_intents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grace_period_pendings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    cancelled_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    operation_log_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grace_period_pendings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guest_folio_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    folio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_folio_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guest_folios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    check_out = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_folios", x => x.id);
                    table.ForeignKey(
                        name: "FK_guest_folios_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hair_salon_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    applies_to_product_ids = table.Column<string>(type: "jsonb", nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hair_salon_campaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomerAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompanyAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_code_at_issue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    vat_regime_at_issue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    einvoice_validation_passed = table.Column<bool>(type: "boolean", nullable: true),
                    einvoice_validation_rule_ids = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TseSignature = table.Column<string>(type: "text", nullable: false),
                    KassenId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TseTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JwsHeader = table.Column<string>(type: "text", nullable: true),
                    JwsPayload = table.Column<string>(type: "text", nullable: true),
                    JwsSignature = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoiceItems = table.Column<string>(type: "jsonb", nullable: true),
                    TaxDetails = table.Column<string>(type: "jsonb", nullable: false),
                    SourcePaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    StornoReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StornoReasonText = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentLogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CartId = table.Column<string>(type: "character varying(50)", nullable: true),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestData = table.Column<string>(type: "jsonb", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TseRequired = table.Column<bool>(type: "boolean", nullable: true),
                    TaxNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    ResponseData = table.Column<string>(type: "jsonb", nullable: true),
                    ProcessingTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    TseSignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLogs", x => x.id);
                    table.ForeignKey(
                        name: "FK_PaymentLogs_carts_CartId",
                        column: x => x.CartId,
                        principalTable: "carts",
                        principalColumn: "CartId");
                    table.ForeignKey(
                        name: "FK_PaymentLogs_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_PaymentLogs_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentSessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CartId = table.Column<string>(type: "character varying(50)", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TseRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TseSignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: true),
                    LastErrorType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastErrorAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_PaymentSessions_carts_CartId",
                        column: x => x.CartId,
                        principalTable: "carts",
                        principalColumn: "CartId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentSessions_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_PaymentSessions_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "jahresbeleg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TotalCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCard = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalVoucher = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalOther = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_20 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_10 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_0 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    MonthlyReferences = table.Column<string>(type: "text", nullable: false),
                    TseSignature = table.Column<string>(type: "text", nullable: true),
                    tse_signature_timestamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tse_certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousSignature = table.Column<string>(type: "text", nullable: true),
                    SignatureChainLength = table.Column<int>(type: "integer", nullable: false),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_december_monatsbeleg = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    daily_closing_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jahresbeleg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jahresbeleg_DailyClosings_daily_closing_id",
                        column: x => x.daily_closing_id,
                        principalTable: "DailyClosings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_jahresbeleg_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kitchen_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    kitchen_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_order_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kitchen_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    table_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ready_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    served_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_kitchen_orders_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kitchen_orders_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "license_reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reminder_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "expiry"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_reminders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    license_plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    license_type = table.Column<int>(type: "integer", nullable: true),
                    custom_valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price_net = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 20.00m),
                    vat_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    price_gross = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    sold_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sold_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    activation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_extended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extended_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    converted_from_trial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    remaining_trial_days_added = table.Column<int>(type: "integer", nullable: true),
                    trial_converted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_sales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    operation_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    maintenance_message = table.Column<string>(type: "text", nullable: true),
                    maintenance_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    maintenance_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    license_valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_grace_period_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_grace_period_used_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_license_sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_license_activation_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_activation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    customer_data_purged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    industry_template_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    industry_template_customizations = table.Column<string>(type: "jsonb", nullable: true),
                    trial_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    trial_reminder_7d_sent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    trial_reminder_3d_sent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    trial_reminder_1d_sent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    trial_converted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_grace_period_ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tse_scu_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tse_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tse_provisioned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenants_license_sales_current_license_sale_id",
                        column: x => x.current_license_sale_id,
                        principalTable: "license_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "localization_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SupportedLanguages = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SupportedCurrencies = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultTimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupportedTimeZones = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultDateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultTimeFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultDecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    NumberFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateFormatOptions = table.Column<string>(type: "jsonb", nullable: false),
                    TimeFormatOptions = table.Column<string>(type: "jsonb", nullable: false),
                    CurrencySymbols = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localization_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_localization_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monatsbeleg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    TotalCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCard = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalVoucher = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalOther = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_20 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_10 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_0 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    DailyClosingCount = table.Column<int>(type: "integer", nullable: false),
                    TseSignature = table.Column<string>(type: "text", nullable: true),
                    tse_signature_timestamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tse_certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousSignature = table.Column<string>(type: "text", nullable: true),
                    SignatureChainLength = table.Column<int>(type: "integer", nullable: false),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    daily_closing_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monatsbeleg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monatsbeleg_DailyClosings_daily_closing_id",
                        column: x => x.daily_closing_id,
                        principalTable: "DailyClosings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_monatsbeleg_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_monatsbeleg_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "offline_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    payload_secrets_protected = table.Column<string>(type: "text", nullable: true),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    server_received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OfflineCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    client_sequence_number = table.Column<int>(type: "integer", nullable: true),
                    clock_drift_warning = table.Column<bool>(type: "boolean", nullable: false),
                    sequence_gap_detected = table.Column<bool>(type: "boolean", nullable: false),
                    sequence_duplicate_detected = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SyncedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiscalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorMessageSafe = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplayAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_offline_transactions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "online_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    customer_device_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    order_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    table_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    delivery_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    order_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "web"),
                    pos_cart_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pushed_to_pos_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    is_undone = table.Column<bool>(type: "boolean", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "payment_method_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    legacy_payment_method_value = table.Column<int>(type: "integer", nullable: false),
                    fiscal_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requires_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    terminal_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    allow_refund = table.Column<bool>(type: "boolean", nullable: false),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_method_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_method_definitions_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_method_definitions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "peppol_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ap_environment = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    legal_entity_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    eidentifier_scheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    eidentifier_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peppol_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_peppol_participants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_modifier_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_selections = table.Column<int>(type: "integer", nullable: false),
                    max_selections = table.Column<int>(type: "integer", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_modifier_groups", x => x.id);
                    table.UniqueConstraint("AK_product_modifier_groups_id_tenant_id", x => new { x.id, x.tenant_id });
                    table.ForeignKey(
                        name: "FK_product_modifier_groups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    action_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    risk_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_risk_scores_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooms_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signature_chain_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c")),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_signature = table.Column<string>(type: "text", nullable: true),
                    last_counter = table.Column<int>(type: "integer", nullable: false),
                    last_turnover_counter_cents = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_chain_state", x => x.id);
                    table.ForeignKey(
                        name: "FK_signature_chain_state_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "split_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_split_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_split_sessions_carts_original_cart_id",
                        column: x => x.original_cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_split_sessions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    license_type = table.Column<int>(type: "integer", nullable: false),
                    amount_net = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    amount_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    amount_gross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_invoices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "support_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assigned_to_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    assigned_to_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_tickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_tickets_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    TaxRates = table.Column<string>(type: "jsonb", nullable: false),
                    ReceiptTemplate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoicePrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReceiptPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AutoBackup = table.Column<bool>(type: "boolean", nullable: false),
                    BackupFrequency = table.Column<int>(type: "integer", nullable: false),
                    MaxBackupFiles = table.Column<int>(type: "integer", nullable: false),
                    LastBackup = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    SmsNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSettings = table.Column<string>(type: "jsonb", nullable: true),
                    SmsSettings = table.Column<string>(type: "jsonb", nullable: true),
                    session_timeout_minutes = table.Column<int>(type: "integer", nullable: false),
                    session_warning_before_timeout_minutes = table.Column<int>(type: "integer", nullable: false),
                    keep_cart_after_timeout = table.Column<bool>(type: "boolean", nullable: false),
                    session_idle_timeout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    online_checkout_payment_methods = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    group_type = table.Column<int>(type: "integer", nullable: true),
                    austrian_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_groups", x => x.id);
                    table.CheckConstraint("CK_tax_groups_rate_range", "rate >= 0 AND rate <= 100");
                    table.ForeignKey(
                        name: "FK_tax_groups_tax_groups_replaced_by",
                        column: x => x.replaced_by,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tax_groups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_customizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    surface = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    primary_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    secondary_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    background_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    text_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    font_family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    favicon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    pages_json = table.Column<string>(type: "text", nullable: false),
                    features_json = table.Column<string>(type: "text", nullable: false),
                    custom_css = table.Column<string>(type: "text", nullable: true),
                    custom_js = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_customizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_customizations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_data_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    export_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    executed_via = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_data_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_data_deletion_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_domains",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    subdomain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verification_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_domains", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_domains_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_limits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_active_registers_per_user = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    max_products_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 10000),
                    max_users_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    daily_max_transactions = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    max_transaction_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 10000m),
                    daily_max_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 50000m),
                    max_backups_per_tenant = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    max_backup_size_mb = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    max_offline_transactions = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_limits", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_limits_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_onboarding_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_onboarding_status", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_onboarding_status_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_service_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "none"),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    template_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    customization = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    artifact_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    custom_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    deactivation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_service_statuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_service_statuses_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    setting_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_settings_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_vertical_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overrides_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_vertical_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_vertical_overrides_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    start_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    venue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    stage_edge = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "top"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_price_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_price_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_price_categories_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tse_backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    encryption_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    device_count = table.Column<int>(type: "integer", nullable: false),
                    chain_count = table.Column<int>(type: "integer", nullable: false),
                    receipt_sequence_count = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_backups", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_backups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_blockchain_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    block_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    block_number = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    signature_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    signature_preview = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_simulated = table.Column<bool>(type: "boolean", nullable: false),
                    network_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_blockchain_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_blockchain_records_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_dr_runbooks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scenario = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    estimated_rto_minutes = table.Column<int>(type: "integer", nullable: false),
                    actual_rto_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_drill = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_tested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_dr_runbooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_dr_runbooks_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_healing_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    max_auto_heal_attempts = table.Column<int>(type: "integer", nullable: false),
                    cooldown_minutes = table.Column<int>(type: "integer", nullable: false),
                    notify_on_heal = table.Column<bool>(type: "boolean", nullable: false),
                    allow_auto_failover = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_healing_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_healing_configurations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    impact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    estimated_savings = table.Column<int>(type: "integer", nullable: false),
                    effort_score = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_applied = table.Column<bool>(type: "boolean", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_dismissed = table.Column<bool>(type: "boolean", nullable: false),
                    dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dismissed_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    rated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_recommendations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_resource_pool_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserved_capacity = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_resource_pool_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_resource_pool_assignments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tse_resource_pool_assignments_tse_resource_pools_pool_id",
                        column: x => x.pool_id,
                        principalTable: "tse_resource_pools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_scaling_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_devices = table.Column<int>(type: "integer", nullable: false),
                    to_devices = table.Column<int>(type: "integer", nullable: false),
                    load_percent = table.Column<double>(type: "double precision", nullable: false),
                    applied = table.Column<bool>(type: "boolean", nullable: false),
                    simulation_only = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_scaling_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_scaling_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_scaling_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_devices = table.Column<int>(type: "integer", nullable: false),
                    max_devices = table.Column<int>(type: "integer", nullable: false),
                    target_transactions_per_device = table.Column<int>(type: "integer", nullable: false),
                    scale_up_threshold = table.Column<double>(type: "double precision", nullable: false),
                    scale_down_threshold = table.Column<double>(type: "double precision", nullable: false),
                    cooldown_minutes = table.Column<int>(type: "integer", nullable: false),
                    auto_provision = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_scaling_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_scaling_policies_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_update_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    update_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    risk_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    to_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    zero_downtime = table.Column<bool>(type: "boolean", nullable: false),
                    devices_touched = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_update_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_update_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_update_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    update_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_update_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_update_states_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_webhooks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    events = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_delivery_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_delivery_success = table.Column<bool>(type: "boolean", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_webhooks_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TseDevices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    LastConnectionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSignatureTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CertificateStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MemoryStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CanCreateInvoices = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    KassenId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FinanzOnlineUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FinanzOnlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFinanzOnlineSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PendingInvoices = table.Column<int>(type: "integer", nullable: false),
                    PendingReports = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApiSecret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Certificate = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PrimaryDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBackup = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsFailoverActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HealthStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HealthScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    LastHealthCheck = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HealthMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailoverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailoverReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FailoverCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScheduledRenewalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignaturkarteProgramCompliantAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignaturkarteProgramCompliantBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SignaturkarteProgramNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseDevices", x => x.id);
                    table.ForeignKey(
                        name: "FK_TseDevices_TseDevices_PrimaryDeviceId",
                        column: x => x.PrimaryDeviceId,
                        principalTable: "TseDevices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TseDevices_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TseDevices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_tenant_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_owner = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tenant_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    masked_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    initial_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    remaining_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    internal_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "FK_vouchers_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vouchers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "online_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    online_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_items_online_orders_online_order_id",
                        column: x => x.online_order_id,
                        principalTable: "online_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "online_order_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    online_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_status_changes_online_orders_online_order_id",
                        column: x => x.online_order_id,
                        principalTable: "online_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "support_ticket_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    author_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_staff_reply = table.Column<bool>(type: "boolean", nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_ticket_messages_support_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_support_ticket_messages_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_tr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    description_de = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    description_tr = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    min_stock_level = table.Column<int>(type: "integer", nullable: false),
                    max_stock_level = table.Column<int>(type: "integer", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    staff_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    imei_tracked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_ticket = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    requires_prescription = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_fiscal_compliant = table.Column<bool>(type: "boolean", nullable: false),
                    fiscal_category_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_taxable = table.Column<bool>(type: "boolean", nullable: false),
                    tax_exemption_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rksv_product_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_sellable_addon = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    original_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("AK_products_id_tenant_id", x => new { x.id, x.tenant_id });
                    table.CheckConstraint("CK_products_cost_non_negative", "cost >= 0");
                    table.CheckConstraint("CK_products_duration_minutes_range", "duration_minutes IS NULL OR (duration_minutes >= 1 AND duration_minutes <= 1440)");
                    table.CheckConstraint("CK_products_min_stock_level_non_negative", "min_stock_level >= 0");
                    table.CheckConstraint("CK_products_price_positive", "price >= 0");
                    table.CheckConstraint("CK_products_stock_quantity_non_negative", "stock_quantity >= 0");
                    table.CheckConstraint("CK_products_tax_rate_range", "tax_rate >= 0 AND tax_rate <= 100");
                    table.ForeignKey(
                        name: "FK_products_categories_category_id_tenant_id",
                        columns: x => new { x.category_id, x.tenant_id },
                        principalTable: "categories",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_products_original_product_id",
                        column: x => x.original_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tax_groups_tax_group_id",
                        column: x => x.tax_group_id,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_data_rights_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    approval_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ready_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    artifact_relative_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    artifact_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    artifact_byte_size = table.Column<long>(type: "bigint", nullable: true),
                    download_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    download_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    view_payload_json = table.Column<string>(type: "text", nullable: true),
                    linked_deletion_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_data_rights_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_data_rights_requests_tenant_data_deletion_requests_l~",
                        column: x => x.linked_deletion_request_id,
                        principalTable: "tenant_data_deletion_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_data_rights_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_event_category_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_event_category_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_event_category_prices_ticket_events_event_id",
                        column: x => x.event_id,
                        principalTable: "ticket_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_event_category_prices_ticket_price_categories_catego~",
                        column: x => x.category_id,
                        principalTable: "ticket_price_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_seats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reserved_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grid_column = table.Column<int>(type: "integer", nullable: true),
                    seat_role = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    companion_of_seat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reserved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_seats", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_seats_ticket_events_event_id",
                        column: x => x.event_id,
                        principalTable: "ticket_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_seats_ticket_price_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "ticket_price_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_seats_ticket_seats_companion_of_seat_id",
                        column: x => x.companion_of_seat_id,
                        principalTable: "ticket_seats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tse_dr_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    runbook_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_automated = table.Column<bool>(type: "boolean", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_dr_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_dr_steps_tse_dr_runbooks_runbook_id",
                        column: x => x.runbook_id,
                        principalTable: "tse_dr_runbooks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_healing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_healing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_healing_rules_tse_healing_configurations_configuration_~",
                        column: x => x.configuration_id,
                        principalTable: "tse_healing_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    response_snippet = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    payload_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_webhook_deliveries_tse_webhooks_webhook_id",
                        column: x => x.webhook_id,
                        principalTable: "tse_webhooks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_anomalies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metric_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_value = table.Column<double>(type: "double precision", nullable: false),
                    expected_value = table.Column<double>(type: "double precision", nullable: false),
                    deviation_percent = table.Column<double>(type: "double precision", nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    suggested_action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_anomalies", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_anomalies_TseDevices_device_id",
                        column: x => x.device_id,
                        principalTable: "TseDevices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tse_anomalies_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    is_backup = table.Column<bool>(type: "boolean", nullable: false),
                    response_time_ms = table.Column<int>(type: "integer", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "tse_healing_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    applied = table.Column<bool>(type: "boolean", nullable: false),
                    health_score_before = table.Column<int>(type: "integer", nullable: false),
                    health_score_after = table.Column<int>(type: "integer", nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_healing_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_healing_history_TseDevices_device_id",
                        column: x => x.device_id,
                        principalTable: "TseDevices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tse_healing_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_incidents_TseDevices_device_id",
                        column: x => x.device_id,
                        principalTable: "TseDevices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tse_incidents_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TseSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SignatureType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TseDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignatureFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JwsHeader = table.Column<string>(type: "text", nullable: true),
                    JwsPayload = table.Column<string>(type: "text", nullable: true),
                    JwsSignature = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TseSignatures_TseDevices_TseDeviceId",
                        column: x => x.TseDeviceId,
                        principalTable: "TseDevices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TseSignatures_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TseSignatures_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "online_order_item_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    online_order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_item_modifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_item_modifiers_online_order_items_online_order~",
                        column: x => x.online_order_item_id,
                        principalTable: "online_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_modifier_group_assignments",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_modifier_group_assignments", x => new { x.product_id, x.modifier_group_id });
                    table.ForeignKey(
                        name: "FK_product_modifier_group_assignments_product_modifier_groups_~",
                        columns: x => new { x.modifier_group_id, x.tenant_id },
                        principalTable: "product_modifier_groups",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_modifier_group_assignments_products_product_id_tena~",
                        columns: x => new { x.product_id, x.tenant_id },
                        principalTable: "products",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_price_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    new_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    old_tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    new_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_rksv_compliant = table.Column<bool>(type: "boolean", nullable: false),
                    rksv_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rksv_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_price_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_price_history_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_price_history_tax_groups_new_tax_group_id",
                        column: x => x.new_tax_group_id,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_price_history_tax_groups_old_tax_group_id",
                        column: x => x.old_tax_group_id,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_price_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_price_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    version = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_price_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_price_versions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_price_versions_tax_groups_tax_group_id",
                        column: x => x.tax_group_id,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_price_versions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    variant_label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    price_delta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_variants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_minibar_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    charged_folio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    charged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_minibar_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_guest_folios_charged_folio_id",
                        column: x => x.charged_folio_id,
                        principalTable: "guest_folios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "split_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    split_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_cart_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seat_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_split_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_split_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_split_items_split_sessions_split_session_id",
                        column: x => x.split_session_id,
                        principalTable: "split_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    new_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_history_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_history_tax_groups_tax_group_id",
                        column: x => x.tax_group_id,
                        principalTable: "tax_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tse_incident_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    performed_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_incident_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_incident_actions_tse_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "tse_incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tse_incident_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tse_incident_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_tse_incident_logs_tse_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "tse_incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offline_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offline_order_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_data = table.Column<string>(type: "jsonb", nullable: false),
                    order_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    synced_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    synced_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sync_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_sync_attempt_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_offline_orders_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offline_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    CashierId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying", maxLength: 50, nullable: false),
                    Steuernummer = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_code_at_issue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    swiss_qr_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    vat_regime_at_issue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    prescription_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    route_from = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    route_to = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    route_km = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    taxi_trip_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TseSignature = table.Column<string>(type: "text", nullable: false),
                    certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrevSignatureValueUsed = table.Column<string>(type: "text", nullable: true),
                    TseTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OriginalPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRefund = table.Column<bool>(type: "boolean", nullable: false),
                    IsStorno = table.Column<bool>(type: "boolean", nullable: false),
                    storno_reason = table.Column<int>(type: "integer", nullable: true),
                    original_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offline_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offline_replay_batch_correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    signature_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    jws_header = table.Column<string>(type: "text", nullable: true),
                    jws_payload = table.Column<string>(type: "text", nullable: true),
                    jws_signature = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxDetails = table.Column<string>(type: "jsonb", nullable: false),
                    PaymentItems = table.Column<string>(type: "jsonb", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "text", nullable: false),
                    IsPrinted = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedBenefitsSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    cancel_idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    finanz_online_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    finanz_online_error = table.Column<string>(type: "text", nullable: true),
                    finanz_online_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    finanz_online_last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finanz_online_retry_count = table.Column<int>(type: "integer", nullable: false),
                    rksv_special_receipt_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rksv_special_receipt_year = table.Column<int>(type: "integer", nullable: true),
                    rksv_special_receipt_month = table.Column<int>(type: "integer", nullable: true),
                    rksv_nullbeleg_acts_as_jahresbeleg = table.Column<bool>(type: "boolean", nullable: false),
                    rksv_is_late_created = table.Column<bool>(type: "boolean", nullable: false),
                    rksv_late_creation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rksv_intended_period_date = table.Column<DateTime>(type: "date", nullable: true),
                    time_sync_warning = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_details_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_details_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_details_offline_transactions_offline_transaction_id",
                        column: x => x.offline_transaction_id,
                        principalTable: "offline_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_imeis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    sold_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warranty_months = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sold_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_imeis", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_imeis_payment_details_sold_payment_id",
                        column: x => x.sold_payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_imeis_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_imeis_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sub_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    qr_code_payload = table.Column<string>(type: "text", nullable: true),
                    signature_value = table.Column<string>(type: "text", nullable: true),
                    prev_signature_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    country_code_at_issue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    vat_regime_at_issue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    signature_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    jws_header = table.Column<string>(type: "text", nullable: true),
                    jws_payload = table.Column<string>(type: "text", nullable: true),
                    jws_signature = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.receipt_id);
                    table.ForeignKey(
                        name: "FK_receipts_payment_details_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_receipts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ticket_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ticket_code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_payment_details_payment_detail_id",
                        column: x => x.payment_detail_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_ticket_events_event_id",
                        column: x => x.event_id,
                        principalTable: "ticket_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_ticket_seats_seat_id",
                        column: x => x.seat_id,
                        principalTable: "ticket_seats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_items",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    line_net = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_items", x => x.item_id);
                    table.ForeignKey(
                        name: "FK_receipt_items_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_receipt_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_tax_lines",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<int>(type: "integer", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_tax_lines", x => x.line_id);
                    table.ForeignKey(
                        name: "FK_receipt_tax_lines_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_receipt_tax_lines_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rksv_special_receipt_finanz_online_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raw_response_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_special_receipt_finanz_online_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_cash_registe~",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_payment_deta~",
                        column: x => x.payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_receipts_rec~",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "voucher_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_payment_details_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "development_mode_settings",
                columns: new[] { "id", "bypass_license", "bypass_ntp_check", "bypass_tse_check", "enabled", "features", "force_online", "simulate_offline", "updated_at_utc", "updated_by_user_id", "valid_days" },
                values: new object[] { 1, true, true, false, true, "[]", true, false, new DateTime(2026, 6, 11, 12, 0, 0, 0, DateTimeKind.Utc), null, 365 });

            migrationBuilder.InsertData(
                table: "vertical_profiles",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "optional_fields", "pos_features", "pos_layout", "required_fields", "updated_at_utc" },
                values: new object[,]
                {
                    { "beherbergung", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.beherbergung.name", "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true,\"variants\":false,\"barcodeScan\":false}", "rooms", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "doctor", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.doctor.name", "{\"customer\":[\"phone\",\"email\",\"birthDate\",\"allergies\",\"chronicConditions\",\"insurance\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":false,\"barcodeScan\":false}", "appointment", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"durationMinutes\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "gastronomy", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.gastronomy.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\"]}", "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "standard", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "gastronomy-tables", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.gastronomyTables.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\"]}", "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "tables", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "hair-salon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.hairSalon.name", "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\",\"staffId\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "appointment", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"durationMinutes\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "handy-shop", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.handyShop.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\",\"imei\",\"serialNumber\",\"brand\",\"model\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "standard", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "mobile-services", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.mobileServices.name", "{\"customer\":[\"email\",\"notes\",\"street\",\"postalCode\",\"city\"],\"product\":[\"description\",\"category\"],\"order\":[\"location\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "appointment", "{\"customer\":[\"name\",\"phone\",\"address\"],\"product\":[\"name\",\"price\",\"serviceDuration\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "retail", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.retail.name", "{\"customer\":[\"email\",\"phone\"],\"product\":[\"category\",\"stock\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":true,\"lotTracking\":false,\"multiBranch\":false,\"barcodeScan\":true}", "standard", "{\"customer\":[\"name\"],\"product\":[\"sku\",\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "taxi", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.taxi.name", "{\"customer\":[\"name\",\"phone\",\"pickupAddress\",\"destinationAddress\"],\"product\":[\"description\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "taxi", "{\"customer\":[],\"product\":[\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "ticket-sales", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.ticketSales.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"room\",\"seat\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":true,\"variants\":false,\"barcodeScan\":false}", "ticket", "{\"customer\":[],\"product\":[\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "vet", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.vet.name", "{\"customer\":[\"phone\",\"email\",\"petSpecies\",\"petBreed\",\"petBirthDate\",\"vaccines\",\"patientNotes\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}", "standard", "{\"customer\":[\"name\",\"petName\"],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_activated_at_utc",
                table: "activated_licenses",
                column: "activated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_is_active",
                table: "activated_licenses",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_last_seen_at_utc",
                table: "activated_licenses",
                column: "last_seen_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_machine_fingerprint_license_key",
                table: "activated_licenses",
                columns: new[] { "machine_fingerprint", "license_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activated_licenses_valid_until_utc",
                table: "activated_licenses",
                column: "valid_until_utc");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_created_at_utc",
                table: "activity_events",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_dedup_key",
                table: "activity_events",
                columns: new[] { "tenant_id", "dedup_key" },
                unique: true,
                filter: "\"dedup_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_severity",
                table: "activity_events",
                columns: new[] { "tenant_id", "severity" });

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_modifier_group_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "modifier_group_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_product_id",
                table: "addon_group_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_product_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "product_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_addon_group_products_tenant_id",
                table: "addon_group_products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_admin_user_feedback_status_created",
                table: "admin_user_feedback",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_admin_user_feedback_submitted_by",
                table: "admin_user_feedback",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_admin_user_feedback_tenant_id",
                table: "admin_user_feedback",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_customer_id",
                table: "appointments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_service_product_id",
                table: "appointments",
                column: "service_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_start_utc",
                table: "appointments",
                column: "start_utc");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id",
                table: "appointments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_appointments_tenant_staff_start",
                table: "appointments",
                columns: new[] { "tenant_id", "staff_id", "start_utc" },
                unique: true,
                filter: "staff_id IS NOT NULL AND status IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_requested_at",
                table: "approval_requests",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_requester_action_tenant",
                table: "approval_requests",
                columns: new[] { "requested_by", "action_type", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_status",
                table: "approval_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_approval_requests_tenant_id",
                table: "approval_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_employee_number",
                table: "AspNetUsers",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_tax_number",
                table: "AspNetUsers",
                column: "tax_number",
                unique: true,
                filter: "tax_number IS NOT NULL AND tax_number <> ''");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_action",
                table: "audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_id",
                table: "audit_logs",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type",
                table: "audit_logs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id_timestamp",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_name_timestamp",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_name", "timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_impersonated_by",
                table: "audit_logs",
                column: "impersonated_by");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_impersonated_tenant",
                table: "audit_logs",
                column: "impersonated_tenant");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id",
                table: "audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_timestamp",
                table: "audit_logs",
                columns: new[] { "tenant_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_timestamp",
                table: "audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_report_schedules_tenant_id_is_active_next_run_utc",
                table: "audit_report_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_created_at_utc",
                table: "auth_sessions",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_tenant_id",
                table: "auth_sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_user_id_revoked_at_utc",
                table: "auth_sessions",
                columns: new[] { "user_id", "revoked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_artifacts_backup_run_id",
                table: "backup_artifacts",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_backup_artifacts_content_hash",
                table: "backup_artifacts",
                column: "content_hash_sha256",
                filter: "content_hash_sha256 IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_backup_artifacts_storage_tier",
                table: "backup_artifacts",
                column: "storage_tier");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_idempotency_key",
                table: "backup_runs",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_lease_expires_stale_reaper",
                table: "backup_runs",
                column: "lease_expires_at_utc",
                filter: "status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_legal_hold",
                table: "backup_runs",
                column: "legal_hold",
                filter: "legal_hold = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_next_retry_at",
                table: "backup_runs",
                column: "next_retry_at_utc",
                filter: "next_retry_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_requested_at",
                table: "backup_runs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_status",
                table: "backup_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_strategy",
                table: "backup_runs",
                column: "strategy");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_tenant_id",
                table: "backup_runs",
                column: "tenant_id",
                filter: "tenant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_backup_schedule_configurations_tenant_id",
                table: "backup_schedule_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backup_verifications_backup_run_id",
                table: "backup_verifications",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_backup_verifications_started_at",
                table: "backup_verifications",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_assignments_benefit_definition_id",
                table: "benefit_assignments",
                column: "benefit_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_assignments_customer_id",
                table: "benefit_assignments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_assignments_customer_id_valid_from_valid_to",
                table: "benefit_assignments",
                columns: new[] { "customer_id", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "IX_benefit_daily_usage_benefit_definition_id",
                table: "benefit_daily_usage",
                column: "benefit_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_daily_usage_customer_id_benefit_definition_id_usage~",
                table: "benefit_daily_usage",
                columns: new[] { "customer_id", "benefit_definition_id", "usage_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_benefit_definitions_allowance_category_id",
                table: "benefit_definitions",
                column: "allowance_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_definitions_code",
                table: "benefit_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_action",
                table: "billing_audit_log",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_sale_id",
                table: "billing_audit_log",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_tenant_id",
                table: "billing_audit_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_timestamp_utc",
                table: "billing_audit_log",
                column: "timestamp_utc");

            migrationBuilder.CreateIndex(
                name: "IX_billing_audit_log_user_id",
                table: "billing_audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_backup_run_id",
                table: "billing_backup_history",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_retention",
                table: "billing_backup_history",
                column: "retention_until_utc");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_sale_id",
                table: "billing_backup_history",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_backup_status",
                table: "billing_backup_history",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_cart_item_modifiers_cart_item_id",
                table: "cart_item_modifiers",
                column: "cart_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_applied_pricing_rule_id",
                table: "cart_items",
                column: "applied_pricing_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId",
                table: "cart_items",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_ApplicationUserId",
                table: "carts",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_CustomerId",
                table: "carts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId",
                table: "carts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_cash_register_open_requests_register_id",
                table: "cash_register_open_requests",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_cash_register_open_requests_status_requested",
                table: "cash_register_open_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "idx_cash_register_open_requests_tenant_id",
                table: "cash_register_open_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_cash_register_open_requests_pending_register_user",
                table: "cash_register_open_requests",
                columns: new[] { "tenant_id", "cash_register_id", "requested_by_user_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_cash_register_transactions_CashRegisterId",
                table: "cash_register_transactions",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_register_transactions_UserId",
                table: "cash_register_transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_assigned_user_id",
                table: "cash_registers",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_CurrentUserId",
                table: "cash_registers",
                column: "CurrentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_Status",
                table: "cash_registers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_tenant_id",
                table: "cash_registers",
                column: "tenant_id",
                unique: true,
                filter: "\"is_default_for_tenant\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_tenant_id_RegisterNumber",
                table: "cash_registers",
                columns: new[] { "tenant_id", "RegisterNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cashier_favorites_product_id",
                table: "cashier_favorites",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_favorites_tenant_id_cashier_id_product_id",
                table: "cashier_favorites",
                columns: new[] { "tenant_id", "cashier_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cashier_favorites_tenant_id_cashier_id_sort_order",
                table: "cashier_favorites",
                columns: new[] { "tenant_id", "cashier_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_cash_register_id_started_at",
                table: "cashier_shifts",
                columns: new[] { "cash_register_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_daily_closing_id",
                table: "cashier_shifts",
                column: "daily_closing_id");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_tenant_id_cashier_id_status",
                table: "cashier_shifts",
                columns: new[] { "tenant_id", "cashier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_SortOrder",
                table: "categories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id_category_key",
                table: "categories",
                columns: new[] { "tenant_id", "category_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ch_receipt_sequences_cash_register_id",
                table: "ch_receipt_sequences",
                column: "cash_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ch_receipt_sequences_tenant_id",
                table: "ch_receipt_sequences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_communication_log_sent_at",
                table: "communication_log",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_communication_log_tenant_id",
                table: "communication_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_company_settings_vertical_profile_id",
                table: "company_settings",
                column: "vertical_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id",
                table: "company_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyRegistrationNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyRegistrationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyTaxNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyTaxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyVatNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyVatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_application_user_id",
                table: "customers",
                column: "application_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id",
                table: "customers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_customer_number",
                table: "customers",
                columns: new[] { "tenant_id", "customer_number" },
                unique: true,
                filter: "customer_number <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_email",
                table: "customers",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "email <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_tax_number",
                table: "customers",
                columns: new[] { "tenant_id", "tax_number" },
                unique: true,
                filter: "tax_number <> ''");

            migrationBuilder.CreateIndex(
                name: "idx_daily_closings_tenant_register_date",
                table: "DailyClosings",
                columns: new[] { "tenant_id", "CashRegisterId", "ClosingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings",
                columns: new[] { "CashRegisterId", "ClosingDate", "ClosingType" },
                unique: true,
                filter: "\"Status\" = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_certificate_thumbprint",
                table: "DailyClosings",
                column: "certificate_thumbprint",
                filter: "\"certificate_thumbprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_tenant_id",
                table: "DailyClosings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_tenant_id_fiskaly_status",
                table: "DailyClosings",
                columns: new[] { "tenant_id", "fiskaly_status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_UserId",
                table: "DailyClosings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_preferences_user_id_tenant_id",
                table: "dashboard_preferences",
                columns: new[] { "user_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_de_receipt_sequences_cash_register_id",
                table: "de_receipt_sequences",
                column: "cash_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_de_receipt_sequences_tenant_id",
                table: "de_receipt_sequences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_de_tse_signatures_payment_details_id",
                table: "de_tse_signatures",
                column: "payment_details_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_de_tse_signatures_tenant_id",
                table: "de_tse_signatures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_demo_requests_created_at_utc",
                table: "demo_requests",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_audit_entries_export_history_id",
                table: "dep_export_audit_entries",
                column: "export_history_id");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_audit_entries_tenant_id_action",
                table: "dep_export_audit_entries",
                columns: new[] { "tenant_id", "action" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_audit_entries_tenant_id_action_at",
                table: "dep_export_audit_entries",
                columns: new[] { "tenant_id", "action_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_compliance_periods_history_id",
                table: "dep_export_compliance_periods",
                column: "history_id",
                filter: "\"history_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_compliance_periods_tenant_id_period_type_period_~",
                table: "dep_export_compliance_periods",
                columns: new[] { "tenant_id", "period_type", "period_start", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_compliance_periods_tenant_id_status",
                table: "dep_export_compliance_periods",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_compliance_scores_tenant_id_calculated_at",
                table: "dep_export_compliance_scores",
                columns: new[] { "tenant_id", "calculated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_download_token",
                table: "dep_export_history",
                column: "download_token",
                unique: true,
                filter: "\"download_token\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_exported_at",
                table: "dep_export_history",
                column: "exported_at");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_retention_until",
                table: "dep_export_history",
                column: "retention_until",
                filter: "\"retention_until\" IS NOT NULL AND \"purged_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_schedule_id",
                table: "dep_export_history",
                column: "schedule_id",
                filter: "\"schedule_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_tenant_id_archived_at",
                table: "dep_export_history",
                columns: new[] { "tenant_id", "archived_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_tenant_id_cash_register_id_exported_at",
                table: "dep_export_history",
                columns: new[] { "tenant_id", "cash_register_id", "exported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_tenant_id_expires_at",
                table: "dep_export_history",
                columns: new[] { "tenant_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_tenant_id_validation_status",
                table: "dep_export_history",
                columns: new[] { "tenant_id", "validation_status" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_schedules_cash_register_id",
                table: "dep_export_schedules",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_schedules_tenant_id_is_active_next_run_at",
                table: "dep_export_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_compliance_signoffs_image_tag_stage_signed_at_utc",
                table: "deployment_compliance_signoffs",
                columns: new[] { "image_tag", "stage", "signed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_history_stage_status",
                table: "deployment_history",
                columns: new[] { "stage", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_history_tenant_id_deployed_at_utc",
                table: "deployment_history",
                columns: new[] { "tenant_id", "deployed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_runs_run_url_stage",
                table: "deployment_runs",
                columns: new[] { "run_url", "stage" });

            migrationBuilder.CreateIndex(
                name: "IX_deployment_runs_stage_updated_at_utc",
                table: "deployment_runs",
                columns: new[] { "stage", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_requests_status_requested",
                table: "digital_service_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_requests_tenant_id",
                table: "digital_service_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_digital_service_requests_pending_tenant_type",
                table: "digital_service_requests",
                columns: new[] { "tenant_id", "service_type" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_subscriptions_next_billing",
                table: "digital_service_subscriptions",
                column: "next_billing_date");

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_subscriptions_status",
                table: "digital_service_subscriptions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_subscriptions_tenant_id",
                table: "digital_service_subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_digital_service_subscriptions_tenant_service_status",
                table: "digital_service_subscriptions",
                columns: new[] { "tenant_id", "service_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_download_history_tenant_id",
                table: "download_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_correlation_id",
                table: "einvoice_submissions",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_invoice_id",
                table: "einvoice_submissions",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_tenant_id",
                table: "einvoice_submissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_eu_receipt_sequences_cash_register_id",
                table: "eu_receipt_sequences",
                column: "cash_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eu_receipt_sequences_tenant_id",
                table: "eu_receipt_sequences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_export_email_deliveries_tenant_id",
                table: "export_email_deliveries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_AggregateType_AggregateId_Mes~",
                table: "finanz_online_outbox_messages",
                columns: new[] { "AggregateType", "AggregateId", "MessageType" });

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_IdempotencyKey",
                table: "finanz_online_outbox_messages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_Status_NextAttemptAt",
                table: "finanz_online_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_TenantId_BranchId_BusinessKey~",
                table: "finanz_online_outbox_messages",
                columns: new[] { "TenantId", "BranchId", "BusinessKey", "MessageType" });

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_TenantId_BranchId_MessageType~",
                table: "finanz_online_outbox_messages",
                columns: new[] { "TenantId", "BranchId", "MessageType", "BusinessKey", "PayloadHash", "Mode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finanz_online_outbox_messages_TransmissionId",
                table: "finanz_online_outbox_messages",
                column: "TransmissionId",
                filter: "\"TransmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_CashRegisterId",
                table: "FinanzOnlineErrors",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_ErrorType_OccurredAt",
                table: "FinanzOnlineErrors",
                columns: new[] { "ErrorType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_ReferenceId",
                table: "FinanzOnlineErrors",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineSubmissions_InvoiceId",
                table: "FinanzOnlineSubmissions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineSubmissions_tenant_id",
                table: "FinanzOnlineSubmissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_receipt_number",
                table: "fiskaly_operation_history",
                column: "receipt_number");

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_retried_from_id",
                table: "fiskaly_operation_history",
                column: "retried_from_id");

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_tenant_id_created_at_utc",
                table: "fiskaly_operation_history",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_tenant_id_error_review_status_cre~",
                table: "fiskaly_operation_history",
                columns: new[] { "tenant_id", "error_review_status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_tenant_id_operation_type_created_~",
                table: "fiskaly_operation_history",
                columns: new[] { "tenant_id", "operation_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_fiskaly_operation_history_tenant_id_status_created_at_utc",
                table: "fiskaly_operation_history",
                columns: new[] { "tenant_id", "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_cash_register_id",
                table: "gateway_payment_intents",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_created_at",
                table: "gateway_payment_intents",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_gateway_payment_intent_id",
                table: "gateway_payment_intents",
                column: "gateway_payment_intent_id");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_payment_id",
                table: "gateway_payment_intents",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_status",
                table: "gateway_payment_intents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_payment_intents_tenant_id",
                table: "gateway_payment_intents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_gateway_payment_intents_tenant_idempotency",
                table: "gateway_payment_intents",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_webhook_events_intent_id",
                table: "gateway_webhook_events",
                column: "intent_id");

            migrationBuilder.CreateIndex(
                name: "ux_gateway_webhook_events_provider_event",
                table: "gateway_webhook_events",
                columns: new[] { "provider", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_generated_receipts_GeneratedAt",
                table: "generated_receipts",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_generated_receipts_Language",
                table: "generated_receipts",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_generated_receipts_TemplateId",
                table: "generated_receipts",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_generated_receipts_TemplateType",
                table: "generated_receipts",
                column: "TemplateType");

            migrationBuilder.CreateIndex(
                name: "idx_grace_period_pendings_tenant_action_entity_status",
                table: "grace_period_pendings",
                columns: new[] { "tenant_id", "action_kind", "entity_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_grace_period_pendings_tenant_id",
                table: "grace_period_pendings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_grace_period_pendings_tenant_status_expires",
                table: "grace_period_pendings",
                columns: new[] { "tenant_id", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_guest_folio_items_folio_id",
                table: "guest_folio_items",
                column: "folio_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folio_items_payment_detail_id",
                table: "guest_folio_items",
                column: "payment_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_customer_id",
                table: "guest_folios",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_room_id",
                table: "guest_folios",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_tenant_id",
                table: "guest_folios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_tenant_room",
                table: "guest_folios",
                columns: new[] { "tenant_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_hair_salon_campaigns_tenant_id",
                table: "hair_salon_campaigns",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_CurrentStock",
                table: "inventory",
                column: "CurrentStock");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_MinStockLevel",
                table: "inventory",
                column: "MinStockLevel");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_ProductId",
                table: "inventory",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_ApplicationUserId",
                table: "inventory_transactions",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_InventoryId",
                table: "inventory_transactions",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_TransactionDate",
                table: "inventory_transactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_TransactionType",
                table: "inventory_transactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_sequences_year_month",
                table: "invoice_sequences",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_company_name_trgm",
                table: "invoices",
                column: "CompanyName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_customer_name_trgm",
                table: "invoices",
                column: "CustomerName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CustomerId",
                table: "invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_invoice_number",
                table: "invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_invoice_number_trgm",
                table: "invoices",
                column: "InvoiceNumber")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_is_active_InvoiceDate",
                table: "invoices",
                columns: new[] { "is_active", "InvoiceDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SourcePaymentId",
                table: "invoices",
                column: "SourcePaymentId",
                unique: true,
                filter: "\"SourcePaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status_InvoiceDate",
                table: "invoices",
                columns: new[] { "Status", "InvoiceDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id",
                table: "invoices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_expiry_at_utc",
                table: "issued_licenses",
                column: "expiry_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_cancelled",
                table: "issued_licenses",
                column: "is_cancelled",
                filter: "is_cancelled = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_deleted",
                table: "issued_licenses",
                column: "is_deleted",
                filter: "is_deleted = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_is_revoked",
                table: "issued_licenses",
                column: "is_revoked",
                filter: "is_revoked = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_issued_at_utc",
                table: "issued_licenses",
                column: "issued_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_license_key",
                table: "issued_licenses",
                column: "license_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_superseded_by_license_id",
                table: "issued_licenses",
                column: "superseded_by_license_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_licenses_transferred_to_license_id",
                table: "issued_licenses",
                column: "transferred_to_license_id");

            migrationBuilder.CreateIndex(
                name: "IX_jahresbeleg_daily_closing_id",
                table: "jahresbeleg",
                column: "daily_closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_jahresbeleg_per_register_year",
                table: "jahresbeleg",
                columns: new[] { "cash_register_id", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jahresbeleg_tenant_id",
                table: "jahresbeleg",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_CashRegisterId",
                table: "jahresbericht_reports",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_OriginalReportId_ReportVersion",
                table: "jahresbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_jahresbericht_reports_ViennaYearStart_ScopeKind_ReportStatus",
                table: "jahresbericht_reports",
                columns: new[] { "ViennaYearStart", "ScopeKind", "ReportStatus" });

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_order_items_kitchen_order_id",
                table: "kitchen_order_items",
                column: "kitchen_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_order_items_product_id",
                table: "kitchen_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_cart_id",
                table: "kitchen_orders",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_cash_register_id",
                table: "kitchen_orders",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_status",
                table: "kitchen_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_orders_tenant_id",
                table: "kitchen_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_activated_at_utc",
                table: "license_activation_attempts",
                column: "activated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_activation_status",
                table: "license_activation_attempts",
                column: "activation_status");

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_license_key_activated_at_utc",
                table: "license_activation_attempts",
                columns: new[] { "license_key", "activated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_license_activation_attempts_machine_fingerprint",
                table: "license_activation_attempts",
                column: "machine_fingerprint");

            migrationBuilder.CreateIndex(
                name: "idx_license_key_mappings_new_license_key",
                table: "license_key_mappings",
                column: "new_license_key");

            migrationBuilder.CreateIndex(
                name: "idx_license_key_mappings_old_license_key",
                table: "license_key_mappings",
                column: "old_license_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_license_sale_id",
                table: "license_reminders",
                column: "license_sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_reminder_date",
                table: "license_reminders",
                column: "reminder_date_utc");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_status",
                table: "license_reminders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_license_reminders_tenant_id",
                table: "license_reminders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_invoice_number",
                table: "license_sales",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales",
                column: "license_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_sold_at",
                table: "license_sales",
                column: "sold_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_status",
                table: "license_sales",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_tenant_id",
                table: "license_sales",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_valid_until_utc",
                table: "license_sales",
                column: "valid_until_utc");

            migrationBuilder.CreateIndex(
                name: "IX_localization_settings_DefaultCurrency",
                table: "localization_settings",
                column: "DefaultCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_localization_settings_DefaultLanguage",
                table: "localization_settings",
                column: "DefaultLanguage");

            migrationBuilder.CreateIndex(
                name: "IX_localization_settings_tenant_id",
                table: "localization_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_maintenance_acks_notification_user",
                table: "maintenance_notification_acknowledgments",
                columns: new[] { "notification_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_maintenance_acks_user_id",
                table: "maintenance_notification_acknowledgments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_maintenance_notifications_end",
                table: "maintenance_notifications",
                column: "scheduled_end_at");

            migrationBuilder.CreateIndex(
                name: "idx_maintenance_notifications_status_start",
                table: "maintenance_notifications",
                columns: new[] { "status", "scheduled_start_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_backup_run_id",
                table: "manual_restore_requests",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_requested_at",
                table: "manual_restore_requests",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_restore_verification_run_id",
                table: "manual_restore_requests",
                column: "restore_verification_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_status",
                table: "manual_restore_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_monatsbeleg_daily_closing_id",
                table: "monatsbeleg",
                column: "daily_closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_monatsbeleg_per_register_month",
                table: "monatsbeleg",
                columns: new[] { "cash_register_id", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monatsbeleg_tenant_id",
                table: "monatsbeleg",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_monatsbeleg_auto_runs_register_id",
                table: "monatsbeleg_auto_runs",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_monatsbeleg_auto_runs_tenant_id",
                table: "monatsbeleg_auto_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_monatsbeleg_auto_runs_register_period",
                table: "monatsbeleg_auto_runs",
                columns: new[] { "tenant_id", "cash_register_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monatsbericht_reports_CashRegisterId",
                table: "monatsbericht_reports",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_monatsbericht_reports_OriginalReportId_ReportVersion",
                table: "monatsbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_monatsbericht_reports_ViennaMonthStart_ScopeKind_ReportStat~",
                table: "monatsbericht_reports",
                columns: new[] { "ViennaMonthStart", "ScopeKind", "ReportStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_CashRegisterId",
                table: "offline_intent_coverage_samples",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_created_at_utc",
                table: "offline_intent_coverage_samples",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_offline_intent_coverage_samples_replay_batch_correlation_id",
                table: "offline_intent_coverage_samples",
                column: "replay_batch_correlation_id");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_created",
                table: "offline_orders",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_expires",
                table: "offline_orders",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_status",
                table: "offline_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_tenant",
                table: "offline_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_tenant_status",
                table: "offline_orders",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_offline_orders_cash_register_id",
                table: "offline_orders",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_orders_synced_payment_id",
                table: "offline_orders",
                column: "synced_payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId",
                table: "offline_transactions",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId_device_id_client_sequen~",
                table: "offline_transactions",
                columns: new[] { "CashRegisterId", "device_id", "client_sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId_payload_hash",
                table: "offline_transactions",
                columns: new[] { "CashRegisterId", "payload_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_Status",
                table: "offline_transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_SyncedPaymentId",
                table: "offline_transactions",
                column: "SyncedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_tenant_id",
                table: "offline_transactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_item_modifiers_item_id",
                table: "online_order_item_modifiers",
                column: "online_order_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_items_order_id",
                table: "online_order_items",
                column: "online_order_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_items_product_id",
                table: "online_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_status_changes_order_changed",
                table: "online_order_status_changes",
                columns: new[] { "online_order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "idx_online_order_status_changes_order_id",
                table: "online_order_status_changes",
                column: "online_order_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_status_changes_tenant_id",
                table: "online_order_status_changes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_created_at",
                table: "online_orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_customer_id",
                table: "online_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_order_status",
                table: "online_orders",
                column: "order_status");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_payment_status",
                table: "online_orders",
                column: "payment_status");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_pos_cart_id",
                table: "online_orders",
                column: "pos_cart_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_stripe_payment_intent_id",
                table: "online_orders",
                column: "stripe_payment_intent_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_tenant_id",
                table: "online_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_tenant_order_number",
                table: "online_orders",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_online_orders_tenant_status_created",
                table: "online_orders",
                columns: new[] { "tenant_id", "order_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_operation_logs_tenant_created",
                table: "operation_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_operation_logs_tenant_id",
                table: "operation_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_operation_logs_tenant_type",
                table: "operation_logs",
                columns: new[] { "tenant_id", "operation_type" });

            migrationBuilder.CreateIndex(
                name: "idx_operation_logs_tenant_undone",
                table: "operation_logs",
                columns: new[] { "tenant_id", "is_undone" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_report_schedules_tenant_id_is_active_next_run_u~",
                table: "operational_report_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ApplicationUserId",
                table: "orders",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId",
                table: "orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_idempotency_key",
                table: "orders",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_source_payment_id",
                table: "orders",
                column: "source_payment_id",
                unique: true,
                filter: "\"source_payment_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_is_preorder_preorder_status",
                table: "orders",
                columns: new[] { "tenant_id", "is_preorder", "preorder_status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_preorder_number",
                table: "orders",
                columns: new[] { "tenant_id", "preorder_number" },
                unique: true,
                filter: "\"is_preorder\" = TRUE AND \"preorder_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_receipt_number",
                table: "orders",
                columns: new[] { "tenant_id", "receipt_number" },
                filter: "\"is_preorder\" = TRUE AND \"receipt_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cancel_idempotency_key",
                table: "payment_details",
                column: "cancel_idempotency_key",
                unique: true,
                filter: "\"cancel_idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cash_register_id_created_at",
                table: "payment_details",
                columns: new[] { "cash_register_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_certificate_thumbprint",
                table: "payment_details",
                column: "certificate_thumbprint",
                filter: "\"certificate_thumbprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_created_at",
                table: "payment_details",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_CustomerId",
                table: "payment_details",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_finanz_online_status",
                table: "payment_details",
                column: "finanz_online_status",
                filter: "\"finanz_online_status\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_idempotency_key",
                table: "payment_details",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_details_jahresbeleg_per_register_year",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_year" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Jahresbeleg' AND \"is_active\" = true AND \"rksv_special_receipt_year\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_details_monatsbeleg_per_register_month",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_year", "rksv_special_receipt_month" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Monatsbeleg' AND \"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_offline_transaction_id",
                table: "payment_details",
                column: "offline_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_original_receipt_id",
                table: "payment_details",
                column: "original_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_PaymentMethod",
                table: "payment_details",
                column: "PaymentMethod");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_ReceiptNumber",
                table: "payment_details",
                column: "ReceiptNumber",
                unique: true,
                filter: "\"ReceiptNumber\" IS NOT NULL AND \"ReceiptNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "ix_payment_details_schlussbeleg_per_register",
                table: "payment_details",
                columns: new[] { "cash_register_id", "rksv_special_receipt_kind" },
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Schlussbeleg' AND \"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_payment_details_startbeleg_per_register",
                table: "payment_details",
                column: "cash_register_id",
                unique: true,
                filter: "\"rksv_special_receipt_kind\" = 'Startbeleg' AND \"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_TseSignature",
                table: "payment_details",
                column: "TseSignature");

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_cash_register_id_code",
                table: "payment_method_definitions",
                columns: new[] { "cash_register_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_tenant_id_cash_register_id_is_ac~",
                table: "payment_method_definitions",
                columns: new[] { "tenant_id", "cash_register_id", "is_active", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_reversal_approvals_idempotency_key",
                table: "payment_reversal_approvals",
                column: "idempotency_key",
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reversal_approvals_tenant_id",
                table: "payment_reversal_approvals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reversal_approvals_tenant_id_payment_id_status",
                table: "payment_reversal_approvals",
                columns: new[] { "tenant_id", "payment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_CartId",
                table: "PaymentLogs",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_CustomerId",
                table: "PaymentLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_InvoiceId",
                table: "PaymentLogs",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_CartId",
                table: "PaymentSessions",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_CustomerId",
                table: "PaymentSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessions_InvoiceId",
                table: "PaymentSessions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_peppol_participants_tenant_id_participant_id",
                table: "peppol_participants",
                columns: new[] { "tenant_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_CashRegisterId",
                table: "periodenbericht_runs",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_CreatedAtUtc",
                table: "periodenbericht_runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_PeriodStartLocalDate_PeriodEndLocalDat~",
                table: "periodenbericht_runs",
                columns: new[] { "PeriodStartLocalDate", "PeriodEndLocalDate", "ScopeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_periodenbericht_runs_QueryParametersHash",
                table: "periodenbericht_runs",
                column: "QueryParametersHash");

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
                name: "IX_pricing_rules_cash_register_id",
                table: "pricing_rules",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_is_active_valid_from_date_valid_to_date",
                table: "pricing_rules",
                columns: new[] { "is_active", "valid_from_date", "valid_to_date" });

            migrationBuilder.CreateIndex(
                name: "IX_product_imeis_product_id",
                table: "product_imeis",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_imeis_sold_payment_id",
                table: "product_imeis",
                column: "sold_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_imeis_tenant_id",
                table: "product_imeis",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_imeis_tenant_product_status",
                table: "product_imeis",
                columns: new[] { "tenant_id", "product_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_product_imeis_tenant_imei",
                table: "product_imeis",
                columns: new[] { "tenant_id", "imei" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_modifier_group_id_tenant~",
                table: "product_modifier_group_assignments",
                columns: new[] { "modifier_group_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_product_id_tenant_id",
                table: "product_modifier_group_assignments",
                columns: new[] { "product_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_group_assignments_tenant_id",
                table: "product_modifier_group_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_modifier_groups_tenant_id",
                table: "product_modifier_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_new_tax_group_id",
                table: "product_price_history",
                column: "new_tax_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_old_tax_group_id",
                table: "product_price_history",
                column: "old_tax_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_product_id",
                table: "product_price_history",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_tenant_id_effective_from",
                table: "product_price_history",
                columns: new[] { "tenant_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_tenant_id_product_id_effective_from",
                table: "product_price_history",
                columns: new[] { "tenant_id", "product_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_product_price_history_tenant_id_product_id_is_active",
                table: "product_price_history",
                columns: new[] { "tenant_id", "product_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_product_price_versions_is_current",
                table: "product_price_versions",
                column: "is_current");

            migrationBuilder.CreateIndex(
                name: "idx_product_price_versions_product_id",
                table: "product_price_versions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_versions_tax_group_id",
                table: "product_price_versions",
                column: "tax_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_price_versions_tenant_id_product_id_is_current",
                table: "product_price_versions",
                columns: new[] { "tenant_id", "product_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "IX_product_price_versions_tenant_id_product_id_valid_from",
                table: "product_price_versions",
                columns: new[] { "tenant_id", "product_id", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_tenant_id",
                table: "product_variants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_product_label",
                table: "product_variants",
                columns: new[] { "product_id", "variant_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_tenant_sku",
                table: "product_variants",
                columns: new[] { "tenant_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_products_imei_tracked",
                table: "products",
                column: "imei_tracked",
                filter: "imei_tracked = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_products_is_ticket",
                table: "products",
                column: "is_ticket",
                filter: "is_ticket = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_products_tenant_name",
                table: "products",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "idx_products_tenant_staff_id",
                table: "products",
                columns: new[] { "tenant_id", "staff_id" },
                filter: "staff_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id_tenant_id",
                table: "products",
                columns: new[] { "category_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_products_original_product_id",
                table: "products",
                column: "original_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tax_group_id",
                table: "products",
                column: "tax_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tax_type",
                table: "products",
                column: "tax_type");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_original_product_id_version",
                table: "products",
                columns: new[] { "tenant_id", "original_product_id", "version" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_receipt_id",
                table: "receipt_items",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_tenant_id",
                table: "receipt_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_sequences_cash_register_id_sequence_date",
                table: "receipt_sequences",
                columns: new[] { "cash_register_id", "sequence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_tax_lines_receipt_id",
                table: "receipt_tax_lines",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_tax_lines_tenant_id",
                table: "receipt_tax_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_templates_IsDefault",
                table: "receipt_templates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_templates_Language",
                table: "receipt_templates",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_templates_TemplateName",
                table: "receipt_templates",
                column: "TemplateName");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_templates_TemplateType",
                table: "receipt_templates",
                column: "TemplateType");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_payment_id",
                table: "receipts",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_receipt_number",
                table: "receipts",
                column: "receipt_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_tenant_id",
                table: "receipts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_session_id_revoked_at_utc_expires_at_utc",
                table: "refresh_tokens",
                columns: new[] { "session_id", "revoked_at_utc", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_created_at_utc",
                table: "refresh_tokens",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_report_pdfs_tenant_id",
                table: "report_pdfs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_pdfs_tenant_id_report_type_report_id_language",
                table: "report_pdfs",
                columns: new[] { "tenant_id", "report_type", "report_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restore_verification_runs_lease_expires_stale_reaper",
                table: "restore_verification_runs",
                column: "lease_expires_at_utc",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_requested_at",
                table: "restore_verification_runs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_source_backup_artifact_id",
                table: "restore_verification_runs",
                column: "source_backup_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_source_backup_run_id",
                table: "restore_verification_runs",
                column: "source_backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_status",
                table: "restore_verification_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_restore_verification_runs_idempotency_key",
                table: "restore_verification_runs",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_created_at",
                table: "risk_scores",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_tenant_id",
                table: "risk_scores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_tenant_resolved_score",
                table: "risk_scores",
                columns: new[] { "tenant_id", "is_resolved", "score" });

            migrationBuilder.CreateIndex(
                name: "idx_risk_scores_user_id",
                table: "risk_scores",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rksv_ausfall_episodes_device_id",
                table: "rksv_ausfall_episodes",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_rksv_ausfall_episodes_outbox_message_id",
                table: "rksv_ausfall_episodes",
                column: "outbox_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_rksv_ausfall_episodes_tenant_id_status",
                table: "rksv_ausfall_episodes",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_rksv_cold_archive_items_archive_run_id",
                table: "rksv_cold_archive_items",
                column: "archive_run_id");

            migrationBuilder.CreateIndex(
                name: "idx_rksv_cold_archive_items_payment_detail_id",
                table: "rksv_cold_archive_items",
                column: "payment_detail_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_rksv_cold_archive_items_tenant_id",
                table: "rksv_cold_archive_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_rksv_cold_archive_runs_created_at",
                table: "rksv_cold_archive_runs",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_cash_registe~",
                table: "rksv_special_receipt_finanz_online_submissions",
                columns: new[] { "cash_register_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_payment_id",
                table: "rksv_special_receipt_finanz_online_submissions",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_receipt_id",
                table: "rksv_special_receipt_finanz_online_submissions",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "idx_role_permission_packages_package_id",
                table: "role_permission_packages",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_permission_packages_role_package",
                table: "role_permission_packages",
                columns: new[] { "role_id", "package_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_charged_folio_id",
                table: "room_minibar_items",
                column: "charged_folio_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_product_id",
                table: "room_minibar_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_minibar_items_room_id",
                table: "room_minibar_items",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_tenant_room_status",
                table: "room_minibar_items",
                columns: new[] { "tenant_id", "room_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_room_minibar_items_tenant_id",
                table: "room_minibar_items",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_tenant_id",
                table: "rooms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_tenant_number",
                table: "rooms",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_cash_register_id",
                table: "signature_chain_state",
                column: "cash_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_tenant_id",
                table: "signature_chain_state",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_items_product_id",
                table: "split_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_items_split_session_id",
                table: "split_items",
                column: "split_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_items_split_session_id_seat_number",
                table: "split_items",
                columns: new[] { "split_session_id", "seat_number" });

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_original_cart_id",
                table: "split_sessions",
                column: "original_cart_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_tenant_id_cashier_id_created_at",
                table: "split_sessions",
                columns: new[] { "tenant_id", "cashier_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_tenant_id_original_cart_id_is_completed",
                table: "split_sessions",
                columns: new[] { "tenant_id", "original_cart_id", "is_completed" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_invoices_invoice_number",
                table: "subscription_invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_invoices_tenant_id",
                table: "subscription_invoices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_invoices_tenant_id_period_start_utc_period_end~",
                table: "subscription_invoices",
                columns: new[] { "tenant_id", "period_start_utc", "period_end_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_support_ticket_messages_tenant_id",
                table: "support_ticket_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_support_ticket_messages_ticket_id",
                table: "support_ticket_messages",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "idx_support_tickets_created_at",
                table: "support_tickets",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_support_tickets_status",
                table: "support_tickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_support_tickets_status_created",
                table: "support_tickets",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_support_tickets_tenant_id",
                table: "support_tickets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_support_tickets_ticket_number",
                table: "support_tickets",
                column: "ticket_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_suspicious_transaction_alerts_tenant_id",
                table: "suspicious_transaction_alerts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_suspicious_transaction_alerts_tenant_id_dedup_key_detected_~",
                table: "suspicious_transaction_alerts",
                columns: new[] { "tenant_id", "dedup_key", "detected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_suspicious_transaction_alerts_tenant_id_status_detected_at_~",
                table: "suspicious_transaction_alerts",
                columns: new[] { "tenant_id", "status", "detected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_tenant_id",
                table: "system_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_tenant_id_CompanyTaxNumber",
                table: "system_settings",
                columns: new[] { "tenant_id", "CompanyTaxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_time_sync_logs_sync_time_utc",
                table: "system_time_sync_logs",
                column: "sync_time_utc");

            migrationBuilder.CreateIndex(
                name: "IX_table_order_item_modifiers_table_order_item_id",
                table: "table_order_item_modifiers",
                column: "table_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_table_order_items_TableOrderId",
                table: "table_order_items",
                column: "TableOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_table_orders_CartId",
                table: "table_orders",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_table_orders_CustomerId",
                table: "table_orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_table_orders_UserId",
                table: "table_orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tagesbericht_reports_CashRegisterId",
                table: "tagesbericht_reports",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_tagesbericht_reports_OriginalReportId_ReportVersion",
                table: "tagesbericht_reports",
                columns: new[] { "OriginalReportId", "ReportVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_tagesbericht_reports_ViennaBusinessDate_CashRegisterId_Repo~",
                table: "tagesbericht_reports",
                columns: new[] { "ViennaBusinessDate", "CashRegisterId", "ReportStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_groups_replaced_by",
                table: "tax_groups",
                column: "replaced_by");

            migrationBuilder.CreateIndex(
                name: "IX_tax_groups_tenant_id",
                table: "tax_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tax_groups_tenant_id_austrian_code",
                table: "tax_groups",
                columns: new[] { "tenant_id", "austrian_code" },
                unique: true,
                filter: "austrian_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tax_groups_tenant_id_is_active_is_default",
                table: "tax_groups",
                columns: new[] { "tenant_id", "is_active", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_history_product_id",
                table: "tax_history",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_tax_history_tax_group_id",
                table: "tax_history",
                column: "tax_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_tax_history_tenant_id_changed_at",
                table: "tax_history",
                columns: new[] { "tenant_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_history_tenant_id_product_id_changed_at",
                table: "tax_history",
                columns: new[] { "tenant_id", "product_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tenant_customizations_tenant_id",
                table: "tenant_customizations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_customizations_tenant_surface",
                table: "tenant_customizations",
                columns: new[] { "tenant_id", "surface" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_deletion_requests_tenant_id",
                table: "tenant_data_deletion_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_deletion_requests_tenant_status",
                table: "tenant_data_deletion_requests",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_rights_requests_download_token",
                table: "tenant_data_rights_requests",
                column: "download_token",
                unique: true,
                filter: "download_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_rights_requests_linked_deletion",
                table: "tenant_data_rights_requests",
                column: "linked_deletion_request_id");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_rights_requests_tenant_id",
                table: "tenant_data_rights_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_data_rights_requests_tenant_type_status",
                table: "tenant_data_rights_requests",
                columns: new[] { "tenant_id", "request_type", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_tenant_domains_host_lookup",
                table: "tenant_domains",
                columns: new[] { "domain", "is_verified", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_tenant_domains_tenant_id",
                table: "tenant_domains",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_domains_domain",
                table: "tenant_domains",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_limits_tenant_id",
                table: "tenant_limits",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_onboarding_status_tenant_id_step",
                table: "tenant_onboarding_status",
                columns: new[] { "tenant_id", "step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tenant_service_statuses_active_enabled",
                table: "tenant_service_statuses",
                columns: new[] { "is_active", "is_enabled" });

            migrationBuilder.CreateIndex(
                name: "idx_tenant_service_statuses_status",
                table: "tenant_service_statuses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_service_statuses_tenant_id",
                table: "tenant_service_statuses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_service_statuses_tenant_type",
                table: "tenant_service_statuses",
                columns: new[] { "tenant_id", "service_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_key",
                table: "tenant_settings",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_tenant_id_key",
                table: "tenant_settings",
                columns: new[] { "tenant_id", "key" },
                unique: true,
                filter: "tenant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_settings_history_requested_at",
                table: "tenant_settings_history",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_settings_history_status",
                table: "tenant_settings_history",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_settings_history_tenant_id",
                table: "tenant_settings_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_settings_history_tenant_type_status",
                table: "tenant_settings_history",
                columns: new[] { "tenant_id", "setting_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_vertical_overrides_tenant_id",
                table: "tenant_vertical_overrides",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tenants_current_license_sale_id",
                table: "tenants",
                column: "current_license_sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_tenants_operation_mode",
                table: "tenants",
                column: "operation_mode");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_license_valid_until",
                table: "tenants",
                column: "license_valid_until_utc");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_status",
                table: "tenants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_trial_ends_at",
                table: "tenants",
                column: "trial_ends_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_trial_status",
                table: "tenants",
                column: "trial_status");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_event_category_prices_category_id",
                table: "ticket_event_category_prices",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_event_category_prices_event_id",
                table: "ticket_event_category_prices",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_event_category_prices_event_category",
                table: "ticket_event_category_prices",
                columns: new[] { "event_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_events_tenant_id",
                table: "ticket_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_price_categories_tenant_id",
                table: "ticket_price_categories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_price_categories_tenant_code",
                table: "ticket_price_categories",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_event_id",
                table: "ticket_redemptions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_payment_detail_id",
                table: "ticket_redemptions",
                column: "payment_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_seat_id",
                table: "ticket_redemptions",
                column: "seat_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_tenant_id",
                table: "ticket_redemptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_ticket_code",
                table: "ticket_redemptions",
                column: "ticket_code");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_redemptions_tenant_code_hash",
                table: "ticket_redemptions",
                columns: new[] { "tenant_id", "ticket_code_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_category_id",
                table: "ticket_seats",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_companion_of_seat_id",
                table: "ticket_seats",
                column: "companion_of_seat_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_event_id",
                table: "ticket_seats",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_seats_event_place",
                table: "ticket_seats",
                columns: new[] { "event_id", "row", "number", "section" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_anomalies_dedup",
                table: "tse_anomalies",
                columns: new[] { "tenant_id", "metric_name", "device_id", "is_resolved" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_anomalies_tenant_detected",
                table: "tse_anomalies",
                columns: new[] { "tenant_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_anomalies_tenant_open_severity",
                table: "tse_anomalies",
                columns: new[] { "tenant_id", "is_resolved", "severity" });

            migrationBuilder.CreateIndex(
                name: "IX_tse_anomalies_device_id",
                table: "tse_anomalies",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_backups_created_at",
                table: "tse_backups",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_tse_backups_tenant_created",
                table: "tse_backups",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_backups_tenant_id",
                table: "tse_backups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_blockchain_records_tenant_created",
                table: "tse_blockchain_records",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_blockchain_records_tenant_sig",
                table: "tse_blockchain_records",
                columns: new[] { "tenant_id", "signature_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_health_samples_device_checked",
                table: "tse_device_health_samples",
                columns: new[] { "device_id", "checked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_health_samples_tenant_checked",
                table: "tse_device_health_samples",
                columns: new[] { "tenant_id", "checked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_dr_runbooks_status",
                table: "tse_dr_runbooks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_tse_dr_runbooks_tenant_created",
                table: "tse_dr_runbooks",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_dr_steps_runbook_id",
                table: "tse_dr_steps",
                column: "runbook_id");

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

            migrationBuilder.CreateIndex(
                name: "idx_tse_gateway_endpoints_config",
                table: "tse_gateway_endpoints",
                column: "config_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_gateway_endpoints_config_provider",
                table: "tse_gateway_endpoints",
                columns: new[] { "config_id", "provider" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_healing_configurations_tenant",
                table: "tse_healing_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_healing_history_device_started",
                table: "tse_healing_history",
                columns: new[] { "device_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_healing_history_tenant_started",
                table: "tse_healing_history",
                columns: new[] { "tenant_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_healing_rules_config",
                table: "tse_healing_rules",
                column: "configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incident_actions_incident_id",
                table: "tse_incident_actions",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incident_logs_incident_id",
                table: "tse_incident_logs",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incidents_device_id",
                table: "tse_incidents",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incidents_severity",
                table: "tse_incidents",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incidents_status",
                table: "tse_incidents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_tse_incidents_tenant_detected",
                table: "tse_incidents",
                columns: new[] { "tenant_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_knowledge_articles_published_faq_views",
                table: "tse_knowledge_articles",
                columns: new[] { "is_published", "is_faq", "view_count" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_knowledge_articles_slug",
                table: "tse_knowledge_articles",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_knowledge_feedback_article",
                table: "tse_knowledge_feedback",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_recommendations_tenant_created",
                table: "tse_recommendations",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_recommendations_tenant_open_code",
                table: "tse_recommendations",
                columns: new[] { "tenant_id", "code", "is_applied", "is_dismissed" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_resource_pool_assignments_pool_id",
                table: "tse_resource_pool_assignments",
                column: "pool_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_resource_pool_assignments_tenant_id",
                table: "tse_resource_pool_assignments",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_resource_pool_rules_pool_id",
                table: "tse_resource_pool_rules",
                column: "pool_id");

            migrationBuilder.CreateIndex(
                name: "idx_tse_resource_pools_name",
                table: "tse_resource_pools",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_tse_resource_pools_type",
                table: "tse_resource_pools",
                column: "pool_type");

            migrationBuilder.CreateIndex(
                name: "idx_tse_scaling_history_tenant_evaluated",
                table: "tse_scaling_history",
                columns: new[] { "tenant_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_scaling_policies_tenant",
                table: "tse_scaling_policies",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_training_progress_user_module",
                table: "tse_training_progress",
                columns: new[] { "user_id", "module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_update_history_tenant_started",
                table: "tse_update_history",
                columns: new[] { "tenant_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_update_states_tenant_type",
                table: "tse_update_states",
                columns: new[] { "tenant_id", "update_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tse_webhook_deliveries_webhook_delivered",
                table: "tse_webhook_deliveries",
                columns: new[] { "webhook_id", "delivered_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tse_webhooks_tenant",
                table: "tse_webhooks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_TseDevices_CashRegisterId",
                table: "TseDevices",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_TseDevices_PrimaryDeviceId",
                table: "TseDevices",
                column: "PrimaryDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TseDevices_SerialNumber",
                table: "TseDevices",
                column: "SerialNumber",
                unique: true);

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
                name: "IX_TseSignatures_CashRegisterId_CreatedAt",
                table: "TseSignatures",
                columns: new[] { "CashRegisterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_Signature_Hash",
                table: "TseSignatures",
                column: "Signature")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_tenant_id",
                table: "TseSignatures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_TseDeviceId",
                table: "TseSignatures",
                column: "TseDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_expires_at",
                table: "user_permission_overrides",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_tenant_id",
                table: "user_permission_overrides",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_user_id",
                table: "user_permission_overrides",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_user_id_tenant_id_permission",
                table: "user_permission_overrides",
                columns: new[] { "user_id", "tenant_id", "permission" });

            migrationBuilder.CreateIndex(
                name: "IX_user_preferences_user_id",
                table: "user_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_tenant_id",
                table: "user_tenant_memberships",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id",
                table: "user_tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id_tenant_id",
                table: "user_tenant_memberships",
                columns: new[] { "user_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_changed_at_utc",
                table: "user_username_history",
                column: "changed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_changed_by_user_id",
                table: "user_username_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_user_id",
                table: "user_username_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_user_id_changed_at_utc",
                table: "user_username_history",
                columns: new[] { "user_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Currency",
                table: "UserSettings",
                column: "Currency");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Language",
                table: "UserSettings",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vertical_profiles_is_active",
                table: "vertical_profiles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_created_at_utc",
                table: "voucher_ledger_entries",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_idempotency_key",
                table: "voucher_ledger_entries",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_payment_id",
                table: "voucher_ledger_entries",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_receipt_id",
                table: "voucher_ledger_entries",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_tenant_id",
                table: "voucher_ledger_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_voucher_id",
                table: "voucher_ledger_entries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_code_hash",
                table: "vouchers",
                column: "code_hash");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_created_at_utc",
                table: "vouchers",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_customer_id",
                table: "vouchers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_expires_at_utc",
                table: "vouchers",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_status",
                table: "vouchers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_tenant_id",
                table: "vouchers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_tenant_id_code_hash",
                table: "vouchers",
                columns: new[] { "tenant_id", "code_hash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_product_modifier_groups_modifier_group~",
                table: "addon_group_products",
                columns: new[] { "modifier_group_id", "tenant_id" },
                principalTable: "product_modifier_groups",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_addon_group_products_products_product_id_tenant_id",
                table: "addon_group_products",
                columns: new[] { "product_id", "tenant_id" },
                principalTable: "products",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_admin_user_feedback_tenants_tenant_id",
                table: "admin_user_feedback",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_customers_customer_id",
                table: "appointments",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_products_service_product_id",
                table: "appointments",
                column: "service_product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_tenants_tenant_id",
                table: "appointments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_tenant_id",
                table: "audit_logs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_auth_sessions_tenants_tenant_id",
                table: "auth_sessions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_backup_artifacts_backup_runs_backup_run_id",
                table: "backup_artifacts",
                column: "backup_run_id",
                principalTable: "backup_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_backup_runs_tenants_tenant_id",
                table: "backup_runs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_backup_schedule_configurations_tenants_tenant_id",
                table: "backup_schedule_configurations",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_benefit_assignments_benefit_definitions_benefit_definition_~",
                table: "benefit_assignments",
                column: "benefit_definition_id",
                principalTable: "benefit_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_benefit_assignments_customers_customer_id",
                table: "benefit_assignments",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_benefit_daily_usage_benefit_definitions_benefit_definition_~",
                table: "benefit_daily_usage",
                column: "benefit_definition_id",
                principalTable: "benefit_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_benefit_daily_usage_customers_customer_id",
                table: "benefit_daily_usage",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_benefit_definitions_categories_allowance_category_id",
                table: "benefit_definitions",
                column: "allowance_category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_license_sales_sale_id",
                table: "billing_audit_log",
                column: "sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_audit_log_tenants_tenant_id",
                table: "billing_audit_log",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_backup_history_license_sales_sale_id",
                table: "billing_backup_history",
                column: "sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_item_modifiers_cart_items_cart_item_id",
                table: "cart_item_modifiers",
                column: "cart_item_id",
                principalTable: "cart_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_carts_CartId",
                table: "cart_items",
                column: "CartId",
                principalTable: "carts",
                principalColumn: "CartId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_pricing_rules_applied_pricing_rule_id",
                table: "cart_items",
                column: "applied_pricing_rule_id",
                principalTable: "pricing_rules",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_carts_customers_CustomerId",
                table: "carts",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_open_requests_cash_registers_cash_register_id",
                table: "cash_register_open_requests",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_open_requests_tenants_tenant_id",
                table: "cash_register_open_requests",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_settings_tenants_tenant_id",
                table: "cash_register_settings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_transactions_cash_registers_CashRegisterId",
                table: "cash_register_transactions",
                column: "CashRegisterId",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_tenants_tenant_id",
                table: "cash_registers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_favorites_products_product_id",
                table: "cashier_favorites",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_favorites_tenants_tenant_id",
                table: "cashier_favorites",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_shifts_DailyClosings_daily_closing_id",
                table: "cashier_shifts",
                column: "daily_closing_id",
                principalTable: "DailyClosings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_shifts_tenants_tenant_id",
                table: "cashier_shifts",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_categories_tenants_tenant_id",
                table: "categories",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ch_receipt_sequences_tenants_tenant_id",
                table: "ch_receipt_sequences",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_communication_log_tenants_tenant_id",
                table: "communication_log",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_settings_tenants_tenant_id",
                table: "company_settings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyClosings_tenants_tenant_id",
                table: "DailyClosings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_de_receipt_sequences_tenants_tenant_id",
                table: "de_receipt_sequences",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_de_tse_signatures_payment_details_payment_details_id",
                table: "de_tse_signatures",
                column: "payment_details_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_de_tse_signatures_tenants_tenant_id",
                table: "de_tse_signatures",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_dep_export_audit_entries_tenants_tenant_id",
                table: "dep_export_audit_entries",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_dep_export_compliance_scores_tenants_tenant_id",
                table: "dep_export_compliance_scores",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_deployment_history_tenants_tenant_id",
                table: "deployment_history",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_digital_service_requests_tenants_tenant_id",
                table: "digital_service_requests",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_digital_service_subscriptions_tenants_tenant_id",
                table: "digital_service_subscriptions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_einvoice_submissions_invoices_invoice_id",
                table: "einvoice_submissions",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_einvoice_submissions_tenants_tenant_id",
                table: "einvoice_submissions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_eu_receipt_sequences_tenants_tenant_id",
                table: "eu_receipt_sequences",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanzOnlineSubmissions_tenants_tenant_id",
                table: "FinanzOnlineSubmissions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gateway_payment_intents_payment_details_payment_id",
                table: "gateway_payment_intents",
                column: "payment_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_gateway_payment_intents_tenants_tenant_id",
                table: "gateway_payment_intents",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_grace_period_pendings_tenants_tenant_id",
                table: "grace_period_pendings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_guest_folio_items_guest_folios_folio_id",
                table: "guest_folio_items",
                column: "folio_id",
                principalTable: "guest_folios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_guest_folio_items_payment_details_payment_detail_id",
                table: "guest_folio_items",
                column: "payment_detail_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_guest_folios_rooms_room_id",
                table: "guest_folios",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_guest_folios_tenants_tenant_id",
                table: "guest_folios",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hair_salon_campaigns_tenants_tenant_id",
                table: "hair_salon_campaigns",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_tenants_tenant_id",
                table: "invoices",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_jahresbeleg_tenants_tenant_id",
                table: "jahresbeleg",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_kitchen_order_items_kitchen_orders_kitchen_order_id",
                table: "kitchen_order_items",
                column: "kitchen_order_id",
                principalTable: "kitchen_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_kitchen_order_items_products_product_id",
                table: "kitchen_order_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_kitchen_orders_tenants_tenant_id",
                table: "kitchen_orders",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_license_reminders_license_sales_license_sale_id",
                table: "license_reminders",
                column: "license_sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_license_reminders_tenants_tenant_id",
                table: "license_reminders",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_license_sales_tenants_tenant_id",
                table: "license_sales",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_offline_orders_payment_details_synced_payment_id",
                table: "offline_orders",
                column: "synced_payment_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_details_receipts_original_receipt_id",
                table: "payment_details",
                column: "original_receipt_id",
                principalTable: "receipts",
                principalColumn: "receipt_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_tenants_tenant_id",
                table: "cash_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_license_sales_tenants_tenant_id",
                table: "license_sales");

            migrationBuilder.DropForeignKey(
                name: "FK_offline_transactions_tenants_tenant_id",
                table: "offline_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_receipts_tenants_tenant_id",
                table: "receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_details_customers_CustomerId",
                table: "payment_details");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_AspNetUsers_CurrentUserId",
                table: "cash_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_AspNetUsers_assigned_user_id",
                table: "cash_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_details_cash_registers_cash_register_id",
                table: "payment_details");

            migrationBuilder.DropForeignKey(
                name: "FK_receipts_payment_details_payment_id",
                table: "receipts");

            migrationBuilder.DropTable(
                name: "activated_licenses");

            migrationBuilder.DropTable(
                name: "activity_event_reads");

            migrationBuilder.DropTable(
                name: "addon_group_products");

            migrationBuilder.DropTable(
                name: "admin_user_feedback");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "audit_report_schedules");

            migrationBuilder.DropTable(
                name: "backup_retention_policy_settings");

            migrationBuilder.DropTable(
                name: "backup_runtime_execution_preferences");

            migrationBuilder.DropTable(
                name: "backup_schedule_configurations");

            migrationBuilder.DropTable(
                name: "backup_settings");

            migrationBuilder.DropTable(
                name: "backup_verifications");

            migrationBuilder.DropTable(
                name: "benefit_assignments");

            migrationBuilder.DropTable(
                name: "benefit_daily_usage");

            migrationBuilder.DropTable(
                name: "billing_audit_log");

            migrationBuilder.DropTable(
                name: "billing_backup_history");

            migrationBuilder.DropTable(
                name: "cart_item_modifiers");

            migrationBuilder.DropTable(
                name: "cash_register_open_requests");

            migrationBuilder.DropTable(
                name: "cash_register_settings");

            migrationBuilder.DropTable(
                name: "cash_register_transactions");

            migrationBuilder.DropTable(
                name: "cashier_favorites");

            migrationBuilder.DropTable(
                name: "cashier_shifts");

            migrationBuilder.DropTable(
                name: "ch_receipt_sequences");

            migrationBuilder.DropTable(
                name: "communication_log");

            migrationBuilder.DropTable(
                name: "company_settings");

            migrationBuilder.DropTable(
                name: "dashboard_preferences");

            migrationBuilder.DropTable(
                name: "de_receipt_sequences");

            migrationBuilder.DropTable(
                name: "de_tse_signatures");

            migrationBuilder.DropTable(
                name: "demo_requests");

            migrationBuilder.DropTable(
                name: "dep_export_audit_entries");

            migrationBuilder.DropTable(
                name: "dep_export_compliance_periods");

            migrationBuilder.DropTable(
                name: "dep_export_compliance_scores");

            migrationBuilder.DropTable(
                name: "dep_export_schedules");

            migrationBuilder.DropTable(
                name: "deployment_compliance_signoffs");

            migrationBuilder.DropTable(
                name: "deployment_history");

            migrationBuilder.DropTable(
                name: "deployment_runs");

            migrationBuilder.DropTable(
                name: "development_mode_settings");

            migrationBuilder.DropTable(
                name: "digital_service_requests");

            migrationBuilder.DropTable(
                name: "digital_service_subscriptions");

            migrationBuilder.DropTable(
                name: "download_history");

            migrationBuilder.DropTable(
                name: "download_security_tickets");

            migrationBuilder.DropTable(
                name: "einvoice_submissions");

            migrationBuilder.DropTable(
                name: "eu_receipt_sequences");

            migrationBuilder.DropTable(
                name: "export_email_deliveries");

            migrationBuilder.DropTable(
                name: "finanz_online_outbox_messages");

            migrationBuilder.DropTable(
                name: "FinanzOnlineErrors");

            migrationBuilder.DropTable(
                name: "FinanzOnlineSubmissions");

            migrationBuilder.DropTable(
                name: "fiskaly_operation_history");

            migrationBuilder.DropTable(
                name: "gateway_payment_intents");

            migrationBuilder.DropTable(
                name: "gateway_webhook_events");

            migrationBuilder.DropTable(
                name: "generated_receipts");

            migrationBuilder.DropTable(
                name: "grace_period_pendings");

            migrationBuilder.DropTable(
                name: "guest_folio_items");

            migrationBuilder.DropTable(
                name: "hair_salon_campaigns");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "invoice_sequences");

            migrationBuilder.DropTable(
                name: "issued_licenses");

            migrationBuilder.DropTable(
                name: "jahresbeleg");

            migrationBuilder.DropTable(
                name: "jahresbericht_reports");

            migrationBuilder.DropTable(
                name: "kitchen_order_items");

            migrationBuilder.DropTable(
                name: "legal_holds");

            migrationBuilder.DropTable(
                name: "license_activation_attempts");

            migrationBuilder.DropTable(
                name: "license_key_mappings");

            migrationBuilder.DropTable(
                name: "license_reminders");

            migrationBuilder.DropTable(
                name: "localization_settings");

            migrationBuilder.DropTable(
                name: "maintenance_notification_acknowledgments");

            migrationBuilder.DropTable(
                name: "manual_restore_requests");

            migrationBuilder.DropTable(
                name: "monatsbeleg");

            migrationBuilder.DropTable(
                name: "monatsbeleg_auto_runs");

            migrationBuilder.DropTable(
                name: "monatsbericht_reports");

            migrationBuilder.DropTable(
                name: "ntp_admin_settings");

            migrationBuilder.DropTable(
                name: "offline_intent_coverage_samples");

            migrationBuilder.DropTable(
                name: "offline_orders");

            migrationBuilder.DropTable(
                name: "online_order_item_modifiers");

            migrationBuilder.DropTable(
                name: "online_order_status_changes");

            migrationBuilder.DropTable(
                name: "operation_logs");

            migrationBuilder.DropTable(
                name: "operational_report_schedules");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "payment_method_definitions");

            migrationBuilder.DropTable(
                name: "payment_reversal_approvals");

            migrationBuilder.DropTable(
                name: "PaymentLogs");

            migrationBuilder.DropTable(
                name: "PaymentMetrics");

            migrationBuilder.DropTable(
                name: "PaymentSessions");

            migrationBuilder.DropTable(
                name: "peppol_participants");

            migrationBuilder.DropTable(
                name: "periodenbericht_runs");

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
                name: "product_imeis");

            migrationBuilder.DropTable(
                name: "product_modifier_group_assignments");

            migrationBuilder.DropTable(
                name: "product_price_history");

            migrationBuilder.DropTable(
                name: "product_price_versions");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "receipt_items");

            migrationBuilder.DropTable(
                name: "receipt_sequences");

            migrationBuilder.DropTable(
                name: "receipt_tax_lines");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "report_pdfs");

            migrationBuilder.DropTable(
                name: "risk_scores");

            migrationBuilder.DropTable(
                name: "rksv_ausfall_episodes");

            migrationBuilder.DropTable(
                name: "rksv_cold_archive_items");

            migrationBuilder.DropTable(
                name: "rksv_runtime_config");

            migrationBuilder.DropTable(
                name: "rksv_special_receipt_finanz_online_submissions");

            migrationBuilder.DropTable(
                name: "role_permission_packages");

            migrationBuilder.DropTable(
                name: "room_minibar_items");

            migrationBuilder.DropTable(
                name: "sensitive_export_approvals");

            migrationBuilder.DropTable(
                name: "signature_chain_state");

            migrationBuilder.DropTable(
                name: "split_items");

            migrationBuilder.DropTable(
                name: "subscription_invoices");

            migrationBuilder.DropTable(
                name: "support_ticket_messages");

            migrationBuilder.DropTable(
                name: "suspicious_transaction_alerts");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "system_time_sync_logs");

            migrationBuilder.DropTable(
                name: "table_order_item_modifiers");

            migrationBuilder.DropTable(
                name: "tagesbericht_reports");

            migrationBuilder.DropTable(
                name: "tax_history");

            migrationBuilder.DropTable(
                name: "tenant_customizations");

            migrationBuilder.DropTable(
                name: "tenant_data_rights_requests");

            migrationBuilder.DropTable(
                name: "tenant_domains");

            migrationBuilder.DropTable(
                name: "tenant_limits");

            migrationBuilder.DropTable(
                name: "tenant_notification_configs");

            migrationBuilder.DropTable(
                name: "tenant_onboarding_status");

            migrationBuilder.DropTable(
                name: "tenant_service_statuses");

            migrationBuilder.DropTable(
                name: "tenant_settings");

            migrationBuilder.DropTable(
                name: "tenant_settings_history");

            migrationBuilder.DropTable(
                name: "tenant_vertical_overrides");

            migrationBuilder.DropTable(
                name: "ticket_event_category_prices");

            migrationBuilder.DropTable(
                name: "ticket_redemptions");

            migrationBuilder.DropTable(
                name: "tse_anomalies");

            migrationBuilder.DropTable(
                name: "tse_backups");

            migrationBuilder.DropTable(
                name: "tse_blockchain_ledger_state");

            migrationBuilder.DropTable(
                name: "tse_blockchain_records");

            migrationBuilder.DropTable(
                name: "tse_device_health_samples");

            migrationBuilder.DropTable(
                name: "tse_dr_steps");

            migrationBuilder.DropTable(
                name: "tse_failover_logs");

            migrationBuilder.DropTable(
                name: "tse_gateway_endpoints");

            migrationBuilder.DropTable(
                name: "tse_healing_history");

            migrationBuilder.DropTable(
                name: "tse_healing_rules");

            migrationBuilder.DropTable(
                name: "tse_health_audit_logs");

            migrationBuilder.DropTable(
                name: "tse_incident_actions");

            migrationBuilder.DropTable(
                name: "tse_incident_logs");

            migrationBuilder.DropTable(
                name: "tse_knowledge_feedback");

            migrationBuilder.DropTable(
                name: "tse_recommendations");

            migrationBuilder.DropTable(
                name: "tse_resource_pool_assignments");

            migrationBuilder.DropTable(
                name: "tse_resource_pool_rules");

            migrationBuilder.DropTable(
                name: "tse_scaling_history");

            migrationBuilder.DropTable(
                name: "tse_scaling_policies");

            migrationBuilder.DropTable(
                name: "tse_training_progress");

            migrationBuilder.DropTable(
                name: "tse_update_history");

            migrationBuilder.DropTable(
                name: "tse_update_states");

            migrationBuilder.DropTable(
                name: "tse_webhook_deliveries");

            migrationBuilder.DropTable(
                name: "TseSignatures");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropTable(
                name: "user_tenant_memberships");

            migrationBuilder.DropTable(
                name: "user_username_history");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "vertical_profile_overrides");

            migrationBuilder.DropTable(
                name: "voucher_ledger_entries");

            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "benefit_definitions");

            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "vertical_profiles");

            migrationBuilder.DropTable(
                name: "dep_export_history");

            migrationBuilder.DropTable(
                name: "receipt_templates");

            migrationBuilder.DropTable(
                name: "inventory");

            migrationBuilder.DropTable(
                name: "kitchen_orders");

            migrationBuilder.DropTable(
                name: "maintenance_notifications");

            migrationBuilder.DropTable(
                name: "restore_verification_runs");

            migrationBuilder.DropTable(
                name: "DailyClosings");

            migrationBuilder.DropTable(
                name: "online_order_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "product_modifier_groups");

            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "rksv_cold_archive_runs");

            migrationBuilder.DropTable(
                name: "permission_packages");

            migrationBuilder.DropTable(
                name: "guest_folios");

            migrationBuilder.DropTable(
                name: "split_sessions");

            migrationBuilder.DropTable(
                name: "support_tickets");

            migrationBuilder.DropTable(
                name: "table_order_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "tenant_data_deletion_requests");

            migrationBuilder.DropTable(
                name: "ticket_seats");

            migrationBuilder.DropTable(
                name: "tse_dr_runbooks");

            migrationBuilder.DropTable(
                name: "tse_gateway_configs");

            migrationBuilder.DropTable(
                name: "tse_healing_configurations");

            migrationBuilder.DropTable(
                name: "tse_incidents");

            migrationBuilder.DropTable(
                name: "tse_knowledge_articles");

            migrationBuilder.DropTable(
                name: "tse_resource_pools");

            migrationBuilder.DropTable(
                name: "tse_webhooks");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "backup_artifacts");

            migrationBuilder.DropTable(
                name: "online_orders");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "table_orders");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "tax_groups");

            migrationBuilder.DropTable(
                name: "ticket_events");

            migrationBuilder.DropTable(
                name: "ticket_price_categories");

            migrationBuilder.DropTable(
                name: "TseDevices");

            migrationBuilder.DropTable(
                name: "backup_runs");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "license_sales");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "cash_registers");

            migrationBuilder.DropTable(
                name: "payment_details");

            migrationBuilder.DropTable(
                name: "offline_transactions");

            migrationBuilder.DropTable(
                name: "receipts");
        }
    }
}
