using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPosCashRegisterFeaturesToSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "pos_auto_open_assigned_closed_register",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "pos_auto_open_sole_closed_register",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "pos_default_auto_open_opening_balance",
                table: "system_settings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "pos_effective_default_on_pos_entry",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pos_auto_open_assigned_closed_register",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "pos_auto_open_sole_closed_register",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "pos_default_auto_open_opening_balance",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "pos_effective_default_on_pos_entry",
                table: "system_settings");
        }
    }
}
