using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260914150000_AddMonatsbelegBlockingPolicy")]
public partial class AddMonatsbelegBlockingPolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "auto_monatsbeleg_enabled",
            table: "company_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "monatsbeleg_blocking_mode",
            table: "company_settings",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Strict");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "auto_monatsbeleg_enabled",
            table: "company_settings");

        migrationBuilder.DropColumn(
            name: "monatsbeleg_blocking_mode",
            table: "company_settings");
    }
}
