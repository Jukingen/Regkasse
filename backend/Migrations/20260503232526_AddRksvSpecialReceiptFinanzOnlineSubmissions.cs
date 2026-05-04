using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRksvSpecialReceiptFinanzOnlineSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rksv_special_receipt_finanz_online_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raw_response_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rksv_special_receipt_finanz_online_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_cash_registe~",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_payment_deta~",
                        column: x => x.payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rksv_special_receipt_finanz_online_submissions_receipts_rec~",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_cash_registe~",
                table: "rksv_special_receipt_finanz_online_submissions",
                columns: new[] { "cash_register_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_payment_id",
                table: "rksv_special_receipt_finanz_online_submissions",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rksv_special_receipt_finanz_online_submissions_receipt_id",
                table: "rksv_special_receipt_finanz_online_submissions",
                column: "receipt_id");

            // Backfill: existing Startbeleg / Jahresbeleg rows get explicit NotRequired tracking (no FinanzOnline call).
            migrationBuilder.Sql(
                """
                INSERT INTO rksv_special_receipt_finanz_online_submissions (
                    id, payment_id, receipt_id, cash_register_id, kind, status,
                    submitted_at_utc, verified_at_utc, last_attempt_at_utc,
                    attempt_count, last_error_code, last_error_message, external_reference, raw_response_snapshot,
                    created_at_utc, updated_at_utc
                )
                SELECT gen_random_uuid(),
                       p.id,
                       r.receipt_id,
                       p.cash_register_id,
                       p.rksv_special_receipt_kind,
                       'NotRequired',
                       NULL, NULL, NULL,
                       0,
                       NULL, NULL, NULL, NULL,
                       CURRENT_TIMESTAMP,
                       CURRENT_TIMESTAMP
                FROM payment_details p
                LEFT JOIN receipts r ON r.payment_id = p.id
                WHERE p.is_active = true
                  AND p.rksv_special_receipt_kind IN ('Startbeleg', 'Jahresbeleg')
                  AND NOT EXISTS (
                    SELECT 1 FROM rksv_special_receipt_finanz_online_submissions x WHERE x.payment_id = p.id
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rksv_special_receipt_finanz_online_submissions");
        }
    }
}
