using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260914140000_AddAutoTagesabschluss")]
public partial class AddAutoTagesabschluss : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "auto_tagesabschluss",
            table: "company_settings",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'{}'::jsonb");

        migrationBuilder.AddColumn<string>(
            name: "trigger",
            table: "DailyClosings",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Manual");

        migrationBuilder.AddColumn<string>(
            name: "cash_count_note",
            table: "DailyClosings",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "cash_count",
            table: "DailyClosings",
            type: "numeric(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "cash_difference",
            table: "DailyClosings",
            type: "numeric(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "open_orders_count",
            table: "DailyClosings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "open_orders_forced",
            table: "DailyClosings",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "auto_tagesabschluss", table: "company_settings");
        migrationBuilder.DropColumn(name: "trigger", table: "DailyClosings");
        migrationBuilder.DropColumn(name: "cash_count_note", table: "DailyClosings");
        migrationBuilder.DropColumn(name: "cash_count", table: "DailyClosings");
        migrationBuilder.DropColumn(name: "cash_difference", table: "DailyClosings");
        migrationBuilder.DropColumn(name: "open_orders_count", table: "DailyClosings");
        migrationBuilder.DropColumn(name: "open_orders_forced", table: "DailyClosings");
    }
}
