using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Fiskaly SIGN AT marker-receipt columns on DailyClosings.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260829120000_AddDailyClosingFiskalySubmission")]
public partial class AddDailyClosingFiskalySubmission : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "DailyClosings"
                ADD COLUMN IF NOT EXISTS fiskaly_receipt_id character varying(80);
            ALTER TABLE "DailyClosings"
                ADD COLUMN IF NOT EXISTS fiskaly_status character varying(20);
            ALTER TABLE "DailyClosings"
                ADD COLUMN IF NOT EXISTS fiskaly_error character varying(500);
            ALTER TABLE "DailyClosings"
                ADD COLUMN IF NOT EXISTS fiskaly_submitted_at_utc timestamp with time zone;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS IX_DailyClosings_tenant_id_fiskaly_status
                ON "DailyClosings" (tenant_id, fiskaly_status);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_DailyClosings_tenant_id_fiskaly_status;");
        migrationBuilder.Sql(
            """
            ALTER TABLE "DailyClosings" DROP COLUMN IF EXISTS fiskaly_submitted_at_utc;
            ALTER TABLE "DailyClosings" DROP COLUMN IF EXISTS fiskaly_error;
            ALTER TABLE "DailyClosings" DROP COLUMN IF EXISTS fiskaly_status;
            ALTER TABLE "DailyClosings" DROP COLUMN IF EXISTS fiskaly_receipt_id;
            """);
    }
}
