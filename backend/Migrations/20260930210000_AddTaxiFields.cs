using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "route_from",
                table: "payment_details",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_to",
                table: "payment_details",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "route_km",
                table: "payment_details",
                type: "decimal(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "taxi_trip_started_at_utc",
                table: "payment_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "taxi_tariff_per_km",
                table: "company_settings",
                type: "decimal(8,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "taxi_tariff_per_km",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "taxi_trip_started_at_utc",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "route_km",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "route_to",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "route_from",
                table: "payment_details");
        }
    }
}
