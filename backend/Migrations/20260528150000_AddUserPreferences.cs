using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Duplicate of <c>user_preferences</c> created in <c>20260527214427_AddManualRestoreRequests</c>.
    /// Kept as a no-op for greenfield + already-applied history compatibility.
    /// </remarks>
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: table created earlier in AddManualRestoreRequests.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: drop ownership remains on AddManualRestoreRequests.Down.
        }
    }
}
