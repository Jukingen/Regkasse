using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Adds <c>day_kind</c> so empty-day Tagesabschluss rows can be distinguished from normal
/// Daily closings. Does not rename <c>ClosingType</c> (Daily/Monthly/Yearly).
/// <c>TransactionCount</c> already exists.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260816140000_AddDailyClosingDayKind")]
public partial class AddDailyClosingDayKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "day_kind",
            table: "DailyClosings",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "normal");

        migrationBuilder.Sql(
            """
            UPDATE "DailyClosings"
            SET day_kind = 'empty'
            WHERE "ClosingType" = 'Daily'
              AND "TransactionCount" = 0
              AND (day_kind IS NULL OR day_kind = 'normal');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "day_kind",
            table: "DailyClosings");
    }
}
