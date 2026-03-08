using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Aligns audit_logs table with AuditLog entity. Drops and recreates the table so that
    /// column names (quoted PascalCase) match EF Core mapping. Fixes 500 on PUT UserManagement and GET AuditLog/user.
    /// </summary>
    public partial class AlignAuditLogsTableWithEntity : Migration
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
    ""Action"" character varying(50) NOT NULL,
    ""Amount"" numeric(18,2),
    ""CorrelationId"" character varying(100),
    ""Description"" character varying(500),
    ""Endpoint"" character varying(100),
    ""EntityId"" uuid,
    ""EntityName"" character varying(100),
    ""EntityType"" character varying(100) NOT NULL,
    ""ErrorDetails"" character varying(500),
    ""HttpMethod"" character varying(10),
    ""HttpStatusCode"" integer,
    ""IpAddress"" character varying(45),
    ""NewValues"" character varying(4000),
    ""Notes"" character varying(500),
    ""OldValues"" character varying(4000),
    ""PaymentMethod"" character varying(50),
    ""ProcessingTimeMs"" double precision,
    ""RequestData"" character varying(4000),
    ""ResponseData"" character varying(4000),
    ""SessionId"" character varying(100) NOT NULL,
    ""Status"" integer NOT NULL,
    ""Timestamp"" timestamp with time zone NOT NULL,
    ""TransactionId"" character varying(100),
    ""TseSignature"" character varying(500),
    ""UserAgent"" character varying(500),
    ""UserId"" character varying(450) NOT NULL,
    ""UserRole"" character varying(50) NOT NULL,
    CONSTRAINT ""PK_audit_logs"" PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_Action"" ON audit_logs (""Action"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_EntityId"" ON audit_logs (""EntityId"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_EntityType"" ON audit_logs (""EntityType"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_Timestamp"" ON audit_logs (""Timestamp"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_UserId"" ON audit_logs (""UserId"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Table recreated by next migration or EnsureAuditLogsTable shape; no backward compatibility required.
            migrationBuilder.Sql("DROP TABLE IF EXISTS audit_logs CASCADE;");
        }
    }
}
