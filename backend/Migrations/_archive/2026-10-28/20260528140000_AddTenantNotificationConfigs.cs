using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Duplicate of <c>tenant_notification_configs</c> created in <c>20260527212105_AddOperationalReportSchedules</c>.
    /// Kept as a no-op for greenfield + already-applied history compatibility.
    /// </remarks>
    public partial class AddTenantNotificationConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: table created earlier in AddOperationalReportSchedules.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: drop ownership remains on AddOperationalReportSchedules.Down.
        }
    }
}
