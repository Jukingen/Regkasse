using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723200000_AddUserPreferencesTimeZoneLanguage")]
public partial class AddUserPreferencesTimeZoneLanguage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "time_zone",
            table: "user_preferences",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "language",
            table: "user_preferences",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE user_preferences
            SET time_zone = COALESCE(time_zone, 'Europe/Vienna'),
                language = COALESCE(language, 'de')
            WHERE time_zone IS NULL OR language IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "time_zone", table: "user_preferences");
        migrationBuilder.DropColumn(name: "language", table: "user_preferences");
    }
}
