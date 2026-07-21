using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReversalApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_reversal_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approval_token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    approval_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_reversal_approvals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_reversal_approvals_idempotency_key",
                table: "payment_reversal_approvals",
                column: "idempotency_key",
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reversal_approvals_tenant_id",
                table: "payment_reversal_approvals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reversal_approvals_tenant_id_payment_id_status",
                table: "payment_reversal_approvals",
                columns: new[] { "tenant_id", "payment_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_reversal_approvals");
        }
    }
}
