using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Duplicate of <c>jahresbeleg</c> created in <c>20260712135503_AddDailyClosingRksvPhase1Columns</c>.
    /// Kept as a no-op for greenfield + already-applied history compatibility.
    /// </remarks>
    public partial class AddJahresbelegTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: table created earlier in AddDailyClosingRksvPhase1Columns.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: drop ownership remains on AddDailyClosingRksvPhase1Columns.Down.
        }
    }
}
