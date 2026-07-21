using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterFeaturesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_register_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_default_on_pos_entry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    auto_open_sole_closed_register = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    auto_open_assigned_closed_register = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_auto_open_opening_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_register_settings", x => x.tenant_id);
                    table.ForeignKey(
                        name: "FK_cash_register_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO cash_register_settings (
                    tenant_id,
                    effective_default_on_pos_entry,
                    auto_open_sole_closed_register,
                    auto_open_assigned_closed_register,
                    default_auto_open_opening_balance,
                    updated_at_utc
                )
                SELECT
                    ss.tenant_id,
                    ss.pos_effective_default_on_pos_entry,
                    ss.pos_auto_open_sole_closed_register,
                    ss.pos_auto_open_assigned_closed_register,
                    ss.pos_default_auto_open_opening_balance,
                    NOW()
                FROM system_settings ss
                ON CONFLICT (tenant_id) DO NOTHING;
                """);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.Sql(
                """
                UPDATE system_settings ss
                SET
                    pos_effective_default_on_pos_entry = crs.effective_default_on_pos_entry,
                    pos_auto_open_sole_closed_register = crs.auto_open_sole_closed_register,
                    pos_auto_open_assigned_closed_register = crs.auto_open_assigned_closed_register,
                    pos_default_auto_open_opening_balance = crs.default_auto_open_opening_balance
                FROM cash_register_settings crs
                WHERE crs.tenant_id = ss.tenant_id;
                """);

            migrationBuilder.DropTable(
                name: "cash_register_settings");
        }
    }
}
