using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class RenameCardTransactionsToCardPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "card_transactions",
                newName: "card_payment_transactions");

            migrationBuilder.RenameColumn(
                name: "payment_details_id",
                table: "card_payment_transactions",
                newName: "payment_id");

            migrationBuilder.RenameColumn(
                name: "gateway_provider",
                table: "card_payment_transactions",
                newName: "gateway");

            migrationBuilder.RenameColumn(
                name: "transaction_id",
                table: "card_payment_transactions",
                newName: "gateway_transaction_id");

            migrationBuilder.RenameColumn(
                name: "last_four_digits",
                table: "card_payment_transactions",
                newName: "card_last4");

            migrationBuilder.RenameColumn(
                name: "confirmed_at_utc",
                table: "card_payment_transactions",
                newName: "completed_at");

            migrationBuilder.RenameIndex(
                name: "PK_card_transactions",
                table: "card_payment_transactions",
                newName: "PK_card_payment_transactions");

            migrationBuilder.RenameIndex(
                name: "IX_card_transactions_cash_register_id",
                table: "card_payment_transactions",
                newName: "IX_card_payment_transactions_cash_register_id");

            migrationBuilder.RenameIndex(
                name: "IX_card_transactions_created_at",
                table: "card_payment_transactions",
                newName: "IX_card_payment_transactions_created_at");

            migrationBuilder.RenameIndex(
                name: "IX_card_transactions_payment_details_id",
                table: "card_payment_transactions",
                newName: "IX_card_payment_transactions_payment_id");

            migrationBuilder.RenameIndex(
                name: "IX_card_transactions_status",
                table: "card_payment_transactions",
                newName: "IX_card_payment_transactions_status");

            migrationBuilder.RenameIndex(
                name: "IX_card_transactions_tenant_id",
                table: "card_payment_transactions",
                newName: "IX_card_payment_transactions_tenant_id");

            migrationBuilder.DropForeignKey(
                name: "FK_card_transactions_payment_details_payment_details_id",
                table: "card_payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_card_transactions_tenants_tenant_id",
                table: "card_payment_transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_card_payment_transactions_payment_details_payment_id",
                table: "card_payment_transactions",
                column: "payment_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_card_payment_transactions_tenants_tenant_id",
                table: "card_payment_transactions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AlterColumn<string>(
                name: "gateway_payment_intent_id",
                table: "card_payment_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "gateway_payment_intent_id",
                table: "card_payment_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_card_payment_transactions_payment_details_payment_id",
                table: "card_payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_card_payment_transactions_tenants_tenant_id",
                table: "card_payment_transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_card_transactions_payment_details_payment_details_id",
                table: "card_payment_transactions",
                column: "payment_id",
                principalTable: "payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_card_transactions_tenants_tenant_id",
                table: "card_payment_transactions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.RenameIndex(
                name: "PK_card_payment_transactions",
                table: "card_payment_transactions",
                newName: "PK_card_transactions");

            migrationBuilder.RenameIndex(
                name: "IX_card_payment_transactions_tenant_id",
                table: "card_payment_transactions",
                newName: "IX_card_transactions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_card_payment_transactions_status",
                table: "card_payment_transactions",
                newName: "IX_card_transactions_status");

            migrationBuilder.RenameIndex(
                name: "IX_card_payment_transactions_payment_id",
                table: "card_payment_transactions",
                newName: "IX_card_transactions_payment_details_id");

            migrationBuilder.RenameIndex(
                name: "IX_card_payment_transactions_created_at",
                table: "card_payment_transactions",
                newName: "IX_card_transactions_created_at");

            migrationBuilder.RenameIndex(
                name: "IX_card_payment_transactions_cash_register_id",
                table: "card_payment_transactions",
                newName: "IX_card_transactions_cash_register_id");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "card_payment_transactions",
                newName: "confirmed_at_utc");

            migrationBuilder.RenameColumn(
                name: "card_last4",
                table: "card_payment_transactions",
                newName: "last_four_digits");

            migrationBuilder.RenameColumn(
                name: "gateway_transaction_id",
                table: "card_payment_transactions",
                newName: "transaction_id");

            migrationBuilder.RenameColumn(
                name: "gateway",
                table: "card_payment_transactions",
                newName: "gateway_provider");

            migrationBuilder.RenameColumn(
                name: "payment_id",
                table: "card_payment_transactions",
                newName: "payment_details_id");

            migrationBuilder.RenameTable(
                name: "card_payment_transactions",
                newName: "card_transactions");
        }
    }
}
