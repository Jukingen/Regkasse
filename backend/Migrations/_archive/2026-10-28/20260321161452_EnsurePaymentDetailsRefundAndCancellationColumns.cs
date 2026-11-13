using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class EnsurePaymentDetailsRefundAndCancellationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DBs created before these PaymentDetails properties were mapped could miss columns; EF queries them on every payment read.
            migrationBuilder.Sql(
                """
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "CancellationReason" character varying(200) NULL;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone NULL;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "OriginalPaymentId" uuid NULL;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "IsRefund" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "RefundReason" character varying(200) NULL;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "RefundAmount" numeric(18,2) NULL;
                ALTER TABLE payment_details ADD COLUMN IF NOT EXISTS "RefundedAt" timestamp with time zone NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "RefundedAt";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "RefundAmount";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "RefundReason";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "IsRefund";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "OriginalPaymentId";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "CancelledAt";
                ALTER TABLE payment_details DROP COLUMN IF EXISTS "CancellationReason";
                """);
        }
    }
}
