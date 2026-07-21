using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "card_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_details_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    gateway_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway_payment_intent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    client_secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    card_brand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_four_digits = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_card_transactions_payment_details_payment_details_id",
                        column: x => x.payment_details_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_card_transactions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_cash_register_id",
                table: "card_transactions",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_created_at",
                table: "card_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_payment_details_id",
                table: "card_transactions",
                column: "payment_details_id");

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_status",
                table: "card_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_tenant_id",
                table: "card_transactions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_transactions");
        }
    }
}
