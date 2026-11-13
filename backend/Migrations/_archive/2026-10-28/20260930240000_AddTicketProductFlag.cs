using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketProductFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_ticket",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_products_is_ticket",
                table: "products",
                column: "is_ticket",
                filter: "is_ticket = TRUE");

            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true,"ticketScan":true}',
                    pos_layout = 'ticket',
                    updated_at_utc = TIMESTAMPTZ '2026-09-30 00:00:00+00'
                WHERE id = 'ticket-sales';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true}',
                    pos_layout = 'queue',
                    updated_at_utc = TIMESTAMPTZ '2026-09-30 00:00:00+00'
                WHERE id = 'ticket-sales';
                """);

            migrationBuilder.DropIndex(
                name: "idx_products_is_ticket",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_ticket",
                table: "products");
        }
    }
}
