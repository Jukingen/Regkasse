using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class OfflineTransactionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "offline_transaction_id",
                table: "payment_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "offline_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SyncedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_offline_transaction_id",
                table: "payment_details",
                column: "offline_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_CashRegisterId",
                table: "offline_transactions",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_Status",
                table: "offline_transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_SyncedPaymentId",
                table: "offline_transactions",
                column: "SyncedPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_details_offline_transactions_offline_transaction_id",
                table: "payment_details",
                column: "offline_transaction_id",
                principalTable: "offline_transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_details_offline_transactions_offline_transaction_id",
                table: "payment_details");

            migrationBuilder.DropTable(
                name: "offline_transactions");

            migrationBuilder.DropIndex(
                name: "IX_payment_details_offline_transaction_id",
                table: "payment_details");

            migrationBuilder.DropColumn(
                name: "offline_transaction_id",
                table: "payment_details");
        }
    }
}
