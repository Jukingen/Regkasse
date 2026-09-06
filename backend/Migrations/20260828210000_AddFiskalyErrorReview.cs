using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Operator triage columns for failed Fiskaly history rows.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260828210000_AddFiskalyErrorReview")]
public partial class AddFiskalyErrorReview : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE fiskaly_operation_history
                ADD COLUMN IF NOT EXISTS error_review_status character varying(16) NOT NULL DEFAULT 'open';
            ALTER TABLE fiskaly_operation_history
                ADD COLUMN IF NOT EXISTS error_reviewed_at_utc timestamp with time zone;
            ALTER TABLE fiskaly_operation_history
                ADD COLUMN IF NOT EXISTS error_reviewed_by_user_id character varying(450);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS IX_fiskaly_operation_history_tenant_id_error_review_status_created_at_utc
                ON fiskaly_operation_history (tenant_id, error_review_status, created_at_utc);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX IF EXISTS IX_fiskaly_operation_history_tenant_id_error_review_status_created_at_utc;");
        migrationBuilder.Sql(
            """
            ALTER TABLE fiskaly_operation_history DROP COLUMN IF EXISTS error_reviewed_by_user_id;
            ALTER TABLE fiskaly_operation_history DROP COLUMN IF EXISTS error_reviewed_at_utc;
            ALTER TABLE fiskaly_operation_history DROP COLUMN IF EXISTS error_review_status;
            """);
    }
}
