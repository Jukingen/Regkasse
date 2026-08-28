using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBypassTseInDevelopmentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive: safe if AddRksvRuntimeConfig already ran without this column.
            // IF NOT EXISTS covers local DBs that already received the column from a patched CreateTable.
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config ADD COLUMN IF NOT EXISTS bypass_tse_in_development boolean NOT NULL DEFAULT false;");

            migrationBuilder.UpdateData(
                table: "development_mode_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "bypass_tse_check",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE rksv_runtime_config DROP COLUMN IF EXISTS bypass_tse_in_development;");

            migrationBuilder.UpdateData(
                table: "development_mode_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "bypass_tse_check",
                value: true);
        }
    }
}
