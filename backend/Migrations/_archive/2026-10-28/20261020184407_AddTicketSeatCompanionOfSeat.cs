using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSeatCompanionOfSeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "companion_of_seat_id",
                table: "ticket_seats",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_companion_of_seat_id",
                table: "ticket_seats",
                column: "companion_of_seat_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_seats_ticket_seats_companion_of_seat_id",
                table: "ticket_seats",
                column: "companion_of_seat_id",
                principalTable: "ticket_seats",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_seats_ticket_seats_companion_of_seat_id",
                table: "ticket_seats");

            migrationBuilder.DropIndex(
                name: "ix_ticket_seats_companion_of_seat_id",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "companion_of_seat_id",
                table: "ticket_seats");
        }
    }
}
