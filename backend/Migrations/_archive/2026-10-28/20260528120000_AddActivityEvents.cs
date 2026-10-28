using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Duplicate of objects already created in <c>20260527212105_AddOperationalReportSchedules</c>.
    /// Kept as a no-op so existing databases that recorded this migration id stay valid; greenfield applies once.
    /// </remarks>
    public partial class AddActivityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: activity_events / activity_event_reads created earlier in AddOperationalReportSchedules.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: drop ownership remains on AddOperationalReportSchedules.Down.
        }
    }
}
