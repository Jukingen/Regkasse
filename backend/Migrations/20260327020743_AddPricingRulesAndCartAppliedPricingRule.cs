using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingRulesAndCartAppliedPricingRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "applied_pricing_rule_id",
                table: "cart_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days_of_week_mask = table.Column<int>(type: "integer", nullable: false),
                    time_window_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    time_start_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_end_minutes = table.Column<int>(type: "integer", nullable: false),
                    target_scope = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<int>(type: "integer", nullable: false),
                    action_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_rules_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_applied_pricing_rule_id",
                table: "cart_items",
                column: "applied_pricing_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_cash_register_id",
                table: "pricing_rules",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_is_active_valid_from_date_valid_to_date",
                table: "pricing_rules",
                columns: new[] { "is_active", "valid_from_date", "valid_to_date" });

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_pricing_rules_applied_pricing_rule_id",
                table: "cart_items",
                column: "applied_pricing_rule_id",
                principalTable: "pricing_rules",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_pricing_rules_applied_pricing_rule_id",
                table: "cart_items");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_applied_pricing_rule_id",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "applied_pricing_rule_id",
                table: "cart_items");
        }
    }
}
