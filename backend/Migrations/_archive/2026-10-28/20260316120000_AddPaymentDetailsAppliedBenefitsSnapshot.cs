using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Adds applied_benefits_snapshot (jsonb, nullable) to payment_details for future customer benefit audit snapshot.
    /// Idempotent; no impact on existing payment, receipt, or daily closing flows.
    /// </summary>
    public partial class AddPaymentDetailsAppliedBenefitsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'AppliedBenefitsSnapshot') THEN
                        ALTER TABLE payment_details ADD COLUMN ""AppliedBenefitsSnapshot"" jsonb NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'AppliedBenefitsSnapshot') THEN
                        ALTER TABLE payment_details DROP COLUMN ""AppliedBenefitsSnapshot"";
                    END IF;
                END $$;
            ");
        }
    }
}
