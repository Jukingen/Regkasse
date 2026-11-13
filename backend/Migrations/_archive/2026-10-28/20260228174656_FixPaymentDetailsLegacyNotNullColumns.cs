using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentDetailsLegacyNotNullColumns : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// payment_details: Model ile eşleşmeyen legacy NOT NULL kolonları nullable yap.
        /// Domain akışı: Payment önce oluşturulur, sonra Invoice. Legacy: Amount, InvoiceId, PaymentDate, vb.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentDate') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""PaymentDate"" DROP NOT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'OrderId') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""OrderId"" DROP NOT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentStatus') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""PaymentStatus"" DROP NOT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'Reference') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""Reference"" DROP NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
