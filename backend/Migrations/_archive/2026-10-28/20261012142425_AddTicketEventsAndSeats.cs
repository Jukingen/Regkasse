using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEventsAndSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "ticket_redemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "seat_id",
                table: "ticket_redemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    start_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    venue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_seats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reserved_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_seats", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_seats_ticket_events_event_id",
                        column: x => x.event_id,
                        principalTable: "ticket_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_event_id",
                table: "ticket_redemptions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_seat_id",
                table: "ticket_redemptions",
                column: "seat_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_events_tenant_id",
                table: "ticket_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_event_id",
                table: "ticket_seats",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_seats_event_place",
                table: "ticket_seats",
                columns: new[] { "event_id", "row", "number", "section" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_redemptions_ticket_events_event_id",
                table: "ticket_redemptions",
                column: "event_id",
                principalTable: "ticket_events",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_redemptions_ticket_seats_seat_id",
                table: "ticket_redemptions",
                column: "seat_id",
                principalTable: "ticket_seats",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_redemptions_ticket_events_event_id",
                table: "ticket_redemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_redemptions_ticket_seats_seat_id",
                table: "ticket_redemptions");

            migrationBuilder.DropTable(
                name: "ticket_seats");

            migrationBuilder.DropTable(
                name: "ticket_events");

            migrationBuilder.DropIndex(
                name: "ix_ticket_redemptions_event_id",
                table: "ticket_redemptions");

            migrationBuilder.DropIndex(
                name: "ix_ticket_redemptions_seat_id",
                table: "ticket_redemptions");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "ticket_redemptions");

            migrationBuilder.DropColumn(
                name: "seat_id",
                table: "ticket_redemptions");
        }
    }
}
