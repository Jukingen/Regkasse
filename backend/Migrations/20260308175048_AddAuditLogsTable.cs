using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Ensures canonical table audit_logs exists. Apply with: dotnet ef database update (from backend folder).
    /// </summary>
    public partial class AddAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: audit_logs table is created by AlignAuditLogsTableWithEntity (single source of DDL).
            // This migration is kept for history; it no longer creates the table to avoid IF NOT EXISTS drift.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: table is managed by AlignAuditLogsTableWithEntity.
        }
    }
}
