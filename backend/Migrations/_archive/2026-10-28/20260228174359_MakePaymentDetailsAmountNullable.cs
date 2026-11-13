using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentDetailsAmountNullable : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// payment_details.Amount: Legacy kolon, model TotalAmount kullanıyor. NOT NULL kaldırılıyor.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'Amount') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""Amount"" DROP NOT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'amount') THEN
                        ALTER TABLE payment_details ALTER COLUMN amount DROP NOT NULL;
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
