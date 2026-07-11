using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyClosingPeriodUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-point cashier_shifts FK before deleting duplicate closings.
            migrationBuilder.Sql(
                """
                WITH keepers AS (
                  SELECT DISTINCT ON ("CashRegisterId", "ClosingDate", "ClosingType") "Id" AS keeper_id,
                         "CashRegisterId",
                         "ClosingDate",
                         "ClosingType"
                  FROM "DailyClosings"
                  WHERE "Status" = 'Completed'
                  ORDER BY "CashRegisterId", "ClosingDate", "ClosingType", "CreatedAt" DESC, "Id" DESC
                ),
                dupes AS (
                  SELECT dc."Id" AS dupe_id, k.keeper_id
                  FROM "DailyClosings" dc
                  INNER JOIN keepers k
                    ON dc."CashRegisterId" = k."CashRegisterId"
                   AND dc."ClosingDate" = k."ClosingDate"
                   AND dc."ClosingType" = k."ClosingType"
                  WHERE dc."Status" = 'Completed'
                    AND dc."Id" <> k.keeper_id
                )
                UPDATE cashier_shifts cs
                SET daily_closing_id = d.keeper_id
                FROM dupes d
                WHERE cs.daily_closing_id = d.dupe_id;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "DailyClosings" AS dc
                WHERE dc."Status" = 'Completed'
                  AND dc."Id" NOT IN (
                    SELECT DISTINCT ON ("CashRegisterId", "ClosingDate", "ClosingType") "Id"
                    FROM "DailyClosings"
                    WHERE "Status" = 'Completed'
                    ORDER BY "CashRegisterId", "ClosingDate", "ClosingType", "CreatedAt" DESC, "Id" DESC
                  );
                """);

            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings",
                columns: new[] { "CashRegisterId", "ClosingDate", "ClosingType" },
                unique: true,
                filter: "\"Status\" = 'Completed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings",
                columns: new[] { "CashRegisterId", "ClosingDate", "ClosingType" });
        }
    }
}
