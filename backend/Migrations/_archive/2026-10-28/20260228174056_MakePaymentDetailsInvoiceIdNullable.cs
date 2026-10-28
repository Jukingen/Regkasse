using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentDetailsInvoiceIdNullable : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// payment_details.InvoiceId: Domain flow creates Payment first, then Invoice. Kolon NOT NULL iken insert başarısız oluyor.
        /// Invoice.SourcePaymentId ile ilişki korunuyor; payment_details.InvoiceId opsiyonel bırakılıyor.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'InvoiceId') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""InvoiceId"" DROP NOT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'invoice_id') THEN
                        ALTER TABLE payment_details ALTER COLUMN invoice_id DROP NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOT NULL geri getirilemez - mevcut NULL kayıtlar olabilir
        }
    }
}
