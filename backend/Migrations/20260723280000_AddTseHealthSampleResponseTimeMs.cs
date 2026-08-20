using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Adds <c>tse_device_health_samples.response_time_ms</c>.
/// This migration previously lacked <see cref="MigrationAttribute"/>, so EF never applied it
/// even though later snapshots already included the column.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260723280000_AddTseHealthSampleResponseTimeMs")]
public partial class AddTseHealthSampleResponseTimeMs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: local DBs may already have later snapshot migrations applied without this column.
        migrationBuilder.Sql("""
            ALTER TABLE tse_device_health_samples
            ADD COLUMN IF NOT EXISTS response_time_ms integer NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tse_device_health_samples
            DROP COLUMN IF EXISTS response_time_ms;
            """);
    }
}
