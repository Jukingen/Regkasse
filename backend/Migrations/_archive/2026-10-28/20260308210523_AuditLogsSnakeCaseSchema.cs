using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Recreates audit_logs with snake_case columns so INSERT/SELECT match EF mapping.
    /// Fixes 42703 (column "Amount" does not exist) when DB had PascalCase or old schema.
    /// </summary>
    public partial class AuditLogsSnakeCaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS audit_logs CASCADE;
CREATE TABLE audit_logs (
    id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    created_by character varying(450),
    updated_at timestamp with time zone,
    updated_by character varying(450),
    is_active boolean NOT NULL,
    session_id character varying(100) NOT NULL,
    user_id character varying(450) NOT NULL,
    user_role character varying(50) NOT NULL,
    action character varying(50) NOT NULL,
    entity_type character varying(100) NOT NULL,
    entity_id uuid,
    entity_name character varying(100),
    old_values character varying(4000),
    new_values character varying(4000),
    request_data character varying(4000),
    response_data character varying(4000),
    status integer NOT NULL,
    timestamp timestamp with time zone NOT NULL,
    description character varying(500),
    notes character varying(500),
    ip_address character varying(45),
    user_agent character varying(500),
    endpoint character varying(100),
    http_method character varying(10),
    http_status_code integer,
    processing_time_ms double precision,
    error_details character varying(500),
    correlation_id character varying(100),
    transaction_id character varying(100),
    amount numeric(18,2),
    payment_method character varying(50),
    tse_signature character varying(500),
    CONSTRAINT ""PK_audit_logs"" PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp ON audit_logs (timestamp);
CREATE INDEX IF NOT EXISTS ix_audit_logs_action ON audit_logs (action);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity_type ON audit_logs (entity_type);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity_id ON audit_logs (entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id ON audit_logs (user_id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS audit_logs CASCADE;");
        }
    }
}
