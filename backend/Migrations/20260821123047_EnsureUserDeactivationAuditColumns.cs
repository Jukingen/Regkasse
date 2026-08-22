using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// RKSV/DSGVO deactivation audit columns. The original 20260308000001_AddUserDeactivationAuditFields file was never
    /// registered as a migration (no Designer/[Migration] attribute), so freshly migrated databases never received these
    /// columns while long-lived ones got them from the companion _Manual.sql. Idempotent so both cases converge.
    /// </summary>
    public partial class EnsureUserDeactivationAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivated_at timestamp with time zone NULL;
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivated_by character varying(450) NULL;
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivation_reason character varying(500) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally not dropped: the columns predate this migration on existing deployments and hold audit data.
        }
    }
}
