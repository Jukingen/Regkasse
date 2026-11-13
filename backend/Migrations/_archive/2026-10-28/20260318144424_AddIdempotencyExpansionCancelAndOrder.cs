using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyExpansionCancelAndOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancel_idempotency_key",
                table: "payment_details",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cancel_idempotency_key",
                table: "payment_details",
                column: "cancel_idempotency_key",
                unique: true,
                filter: "\"cancel_idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_idempotency_key",
                table: "orders",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_cancel_idempotency_key",
                table: "payment_details");

            migrationBuilder.DropIndex(
                name: "IX_orders_idempotency_key",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancel_idempotency_key",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "orders");
        }
    }
}
