using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Adds benefit_daily_usage table for daily free-allowance tracking and allowance_category_id to benefit_definitions.
    /// Idempotent; no change to payment/receipt/TSE flows until PaymentService uses them.
    /// </summary>
    public partial class AddBenefitDailyUsageAndAllowanceCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'benefit_definitions' AND column_name = 'allowance_category_id') THEN
                        ALTER TABLE benefit_definitions ADD COLUMN allowance_category_id uuid NULL;
                        ALTER TABLE benefit_definitions ADD CONSTRAINT fk_benefit_definitions_allowance_category
                            FOREIGN KEY (allowance_category_id) REFERENCES categories (id) ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS benefit_daily_usage (
                    id uuid NOT NULL PRIMARY KEY,
                    customer_id uuid NOT NULL,
                    benefit_definition_id uuid NOT NULL,
                    usage_date date NOT NULL,
                    quantity_used integer NOT NULL DEFAULT 0,
                    version integer NOT NULL DEFAULT 0,
                    CONSTRAINT fk_benefit_daily_usage_customer FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
                    CONSTRAINT fk_benefit_daily_usage_benefit_definition FOREIGN KEY (benefit_definition_id) REFERENCES benefit_definitions (id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_benefit_daily_usage_customer_definition_date
                    ON benefit_daily_usage (customer_id, benefit_definition_id, usage_date);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_benefit_daily_usage_customer_definition_date;
                DROP TABLE IF EXISTS benefit_daily_usage;
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'benefit_definitions' AND column_name = 'allowance_category_id') THEN
                        ALTER TABLE benefit_definitions DROP CONSTRAINT IF EXISTS fk_benefit_definitions_allowance_category;
                        ALTER TABLE benefit_definitions DROP COLUMN allowance_category_id;
                    END IF;
                END $$;
            ");
        }
    }
}
