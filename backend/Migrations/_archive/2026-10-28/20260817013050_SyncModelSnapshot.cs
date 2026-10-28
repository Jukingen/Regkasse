using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Snapshot-only sync. Schema for subscription invoices, support tickets, onboarding,
    /// trial columns, and license key mappings already shipped in earlier 202608* migrations.
    /// Applying those operations again would fail on existing databases.
    /// </summary>
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
