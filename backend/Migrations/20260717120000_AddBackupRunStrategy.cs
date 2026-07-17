using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
public partial class AddBackupRunStrategy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "strategy",
            table: "backup_runs",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.Sql(
            """
            UPDATE backup_runs
            SET strategy = CASE
                WHEN tenant_id IS NOT NULL THEN 0
                ELSE 1
            END;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_backup_runs_strategy",
            table: "backup_runs",
            column: "strategy");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_backup_runs_strategy",
            table: "backup_runs");

        migrationBuilder.DropColumn(
            name: "strategy",
            table: "backup_runs");
    }
}
