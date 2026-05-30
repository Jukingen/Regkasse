using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <remarks>Schema is created in <see cref="AddSuspiciousTransactionAlerts"/>; this migration is a no-op placeholder.</remarks>
    public partial class AddUserPermissionOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tables and indexes were added in 20260530220922_AddSuspiciousTransactionAlerts.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — down for 20260530220922 drops the shared tables.
        }
    }
}
