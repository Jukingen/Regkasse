using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupRuntimeExecutionPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_runtime_execution_preferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runtime_execution_preferences", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "backup_runtime_execution_preferences",
                columns: new[] { "Id", "Mode", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "backup_runtime_execution_preferences",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropTable(
                name: "backup_runtime_execution_preferences");
        }
    }
}
