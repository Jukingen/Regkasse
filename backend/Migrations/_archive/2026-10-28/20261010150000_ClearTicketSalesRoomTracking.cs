using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Ticket sales no longer seeds lodging <c>roomTracking</c>. Seat and room stay optional
    /// product fields. Beherbergung keeps <c>roomTracking</c>. Hub override rows are not rewritten.
    /// </summary>
    public partial class ClearTicketSalesRoomTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false,"ticketScan":true}',
                    updated_at_utc = TIMESTAMPTZ '2026-10-10 00:00:00+00'
                WHERE id = 'ticket-sales';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true,"ticketScan":true}',
                    updated_at_utc = TIMESTAMPTZ '2026-09-30 00:00:00+00'
                WHERE id = 'ticket-sales';
                """);
        }
    }
}
