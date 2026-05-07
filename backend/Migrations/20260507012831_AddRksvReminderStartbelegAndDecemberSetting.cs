using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRksvReminderStartbelegAndDecemberSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "use_december_monatsbeleg_as_jahresbeleg",
                table: "company_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "startbeleg_created_at",
                table: "cash_registers",
                type: "timestamptz",
                nullable: true);

            // Backfill Startbeleg marker from existing fiscal rows (policy column aligns with historical receipts).
            migrationBuilder.Sql(
                """
                UPDATE cash_registers AS cr
                SET startbeleg_created_at = s.first_sb
                FROM (
                    SELECT cash_register_id AS cid, MIN(created_at) AS first_sb
                    FROM payment_details
                    WHERE is_active = true
                      AND rksv_special_receipt_kind = 'Startbeleg'
                    GROUP BY cash_register_id
                ) AS s
                WHERE cr.id = s.cid
                  AND cr.startbeleg_created_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "use_december_monatsbeleg_as_jahresbeleg",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "startbeleg_created_at",
                table: "cash_registers");
        }
    }
}
