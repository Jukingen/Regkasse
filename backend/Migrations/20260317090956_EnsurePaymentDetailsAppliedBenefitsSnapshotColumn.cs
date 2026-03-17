using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Adds AppliedBenefitsSnapshot (jsonb, nullable) to payment_details when missing.
    /// Idempotent so safe to run if column already exists (e.g. after manual fix).
    /// </summary>
    public partial class EnsurePaymentDetailsAppliedBenefitsSnapshotColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'payment_details'
                        AND LOWER(column_name) = 'appliedbenefitssnapshot'
                    ) THEN
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
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'payment_details'
                        AND LOWER(column_name) = 'appliedbenefitssnapshot'
                    ) THEN
                        ALTER TABLE payment_details DROP COLUMN ""AppliedBenefitsSnapshot"";
                    END IF;
                END $$;
            ");
        }
    }
}
