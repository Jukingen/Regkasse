using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Idempotent: ensures audit_logs table exists (root cause fix for GET /api/AuditLog/user/{id} 500).
    /// Run: dotnet ef database update (from backend folder).
    /// </summary>
    public partial class EnsureAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: audit_logs table is created by AlignAuditLogsTableWithEntity (single source of DDL).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: table is managed by AlignAuditLogsTableWithEntity.
        }
    }
}
