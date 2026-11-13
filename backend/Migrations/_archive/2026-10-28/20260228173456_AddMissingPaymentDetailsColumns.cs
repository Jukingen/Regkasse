using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPaymentDetailsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // payment_details: Eksik kolonları idempotent ekle (PostgreSQL)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'CashierId') THEN
                        ALTER TABLE payment_details ADD COLUMN ""CashierId"" character varying(100) NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'CustomerName') THEN
                        ALTER TABLE payment_details ADD COLUMN ""CustomerName"" character varying(100) NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'IsPrinted') THEN
                        ALTER TABLE payment_details ADD COLUMN ""IsPrinted"" boolean NULL DEFAULT false;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'KassenId') THEN
                        ALTER TABLE payment_details ADD COLUMN ""KassenId"" character varying(50) NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentItems') THEN
                        ALTER TABLE payment_details ADD COLUMN ""PaymentItems"" jsonb NULL DEFAULT '[]';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'ReceiptNumber') THEN
                        ALTER TABLE payment_details ADD COLUMN ""ReceiptNumber"" character varying(50) NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'Steuernummer') THEN
                        ALTER TABLE payment_details ADD COLUMN ""Steuernummer"" character varying(12) NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TableNumber') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TableNumber"" integer NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TaxAmount') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TaxAmount"" decimal(18,2) NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TaxDetails') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TaxDetails"" jsonb NULL DEFAULT '{}';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TotalAmount') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TotalAmount"" decimal(18,2) NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TseSignature') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TseSignature"" character varying(2000) NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TseTimestamp') THEN
                        ALTER TABLE payment_details ADD COLUMN ""TseTimestamp"" timestamp with time zone NULL;
                    END IF;
                    -- PaymentMethod: DB integer ise EF varchar beklediği için character varying'e çevir
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentMethod') AND
                       (SELECT data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentMethod') = 'integer' THEN
                        ALTER TABLE payment_details ALTER COLUMN ""PaymentMethod"" TYPE character varying(50) USING ""PaymentMethod""::text;
                        ALTER TABLE payment_details ALTER COLUMN ""PaymentMethod"" SET DEFAULT '0';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: eklenen kolonları kaldır (mevcut veri kaybı riski)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TseTimestamp') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TseTimestamp"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TseSignature') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TseSignature"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TotalAmount') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TotalAmount"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TaxDetails') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TaxDetails"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TaxAmount') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TaxAmount"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'TableNumber') THEN
                        ALTER TABLE payment_details DROP COLUMN ""TableNumber"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'Steuernummer') THEN
                        ALTER TABLE payment_details DROP COLUMN ""Steuernummer"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'ReceiptNumber') THEN
                        ALTER TABLE payment_details DROP COLUMN ""ReceiptNumber"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'PaymentItems') THEN
                        ALTER TABLE payment_details DROP COLUMN ""PaymentItems"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'KassenId') THEN
                        ALTER TABLE payment_details DROP COLUMN ""KassenId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'IsPrinted') THEN
                        ALTER TABLE payment_details DROP COLUMN ""IsPrinted"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'CustomerName') THEN
                        ALTER TABLE payment_details DROP COLUMN ""CustomerName"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'CashierId') THEN
                        ALTER TABLE payment_details DROP COLUMN ""CashierId"";
                    END IF;
                END $$;
            ");
        }
    }
}
