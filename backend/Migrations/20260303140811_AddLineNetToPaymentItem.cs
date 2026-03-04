using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddLineNetToPaymentItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // payment_items may not exist (items stored in payment_details.PaymentItems JSON); add column only if table exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'payment_items') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_items' AND column_name = 'LineNet') THEN
                            ALTER TABLE payment_items ADD COLUMN ""LineNet"" numeric(18,2) NOT NULL DEFAULT 0;
                        END IF;
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'payment_items') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_items' AND column_name = 'LineNet') THEN
                            ALTER TABLE payment_items DROP COLUMN ""LineNet"";
                        END IF;
                    END IF;
                END $$;");
        }
    }
}
