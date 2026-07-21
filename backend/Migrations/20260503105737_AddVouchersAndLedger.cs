using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchersAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    masked_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    initial_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    remaining_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "FK_vouchers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_payment_details_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_ledger_entries_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_created_at_utc",
                table: "voucher_ledger_entries",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_idempotency_key",
                table: "voucher_ledger_entries",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_payment_id",
                table: "voucher_ledger_entries",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_receipt_id",
                table: "voucher_ledger_entries",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_tenant_id",
                table: "voucher_ledger_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_ledger_entries_voucher_id",
                table: "voucher_ledger_entries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_code_hash",
                table: "vouchers",
                column: "code_hash");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_expires_at_utc",
                table: "vouchers",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_status",
                table: "vouchers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_tenant_id",
                table: "vouchers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_tenant_id_code_hash",
                table: "vouchers",
                columns: new[] { "tenant_id", "code_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_ledger_entries");

            migrationBuilder.DropTable(
                name: "vouchers");
        }
    }
}
