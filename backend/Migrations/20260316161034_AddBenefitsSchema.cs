using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Idempotently ensures benefit_definitions, benefit_assignments, and benefit_daily_usage exist.
    /// Use when earlier benefit migrations (AddBenefitDefinitionAndBenefitAssignment, AddBenefitDailyUsageAndAllowanceCategoryId)
    /// were not applied or tables are missing. Safe to run when tables already exist.
    /// </summary>
    public partial class AddBenefitsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS benefit_definitions (
                    id uuid NOT NULL PRIMARY KEY,
                    code character varying(50) NOT NULL,
                    name character varying(100) NOT NULL,
                    benefit_kind integer NOT NULL,
                    percentage_value numeric(5,2) NULL,
                    allowance_quantity integer NULL,
                    allowance_scope character varying(50) NULL,
                    allowance_category_id uuid NULL,
                    buy_x_quantity integer NULL,
                    get_y_quantity integer NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NULL,
                    created_by character varying(450) NULL,
                    updated_by character varying(450) NULL,
                    is_active boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_benefit_definitions_code ON benefit_definitions (code);

                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'benefit_definitions' AND column_name = 'allowance_category_id') THEN
                        ALTER TABLE benefit_definitions ADD COLUMN allowance_category_id uuid NULL;
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS ix_benefit_definitions_allowance_category_id ON benefit_definitions (allowance_category_id);
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_benefit_definitions_allowance_category') THEN
                        ALTER TABLE benefit_definitions
                            ADD CONSTRAINT fk_benefit_definitions_allowance_category
                            FOREIGN KEY (allowance_category_id) REFERENCES categories (id) ON DELETE SET NULL;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS benefit_assignments (
                    id uuid NOT NULL PRIMARY KEY,
                    benefit_definition_id uuid NOT NULL,
                    customer_id uuid NOT NULL,
                    valid_from timestamp with time zone NOT NULL,
                    valid_to timestamp with time zone NULL,
                    priority integer NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NULL,
                    created_by character varying(450) NULL,
                    updated_by character varying(450) NULL,
                    is_active boolean NOT NULL DEFAULT true,
                    CONSTRAINT fk_benefit_assignments_benefit_definition FOREIGN KEY (benefit_definition_id) REFERENCES benefit_definitions (id) ON DELETE RESTRICT,
                    CONSTRAINT fk_benefit_assignments_customer FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ix_benefit_assignments_benefit_definition_id ON benefit_assignments (benefit_definition_id);
                CREATE INDEX IF NOT EXISTS ix_benefit_assignments_customer_id ON benefit_assignments (customer_id);
                CREATE INDEX IF NOT EXISTS ix_benefit_assignments_customer_valid ON benefit_assignments (customer_id, valid_from, valid_to);

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
                CREATE UNIQUE INDEX IF NOT EXISTS ix_benefit_daily_usage_customer_definition_date ON benefit_daily_usage (customer_id, benefit_definition_id, usage_date);
                CREATE INDEX IF NOT EXISTS ix_benefit_daily_usage_benefit_definition_id ON benefit_daily_usage (benefit_definition_id);
                CREATE INDEX IF NOT EXISTS ix_benefit_daily_usage_customer_id ON benefit_daily_usage (customer_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_benefit_daily_usage_customer_id;
                DROP INDEX IF EXISTS ix_benefit_daily_usage_benefit_definition_id;
                DROP INDEX IF EXISTS ix_benefit_daily_usage_customer_definition_date;
                DROP TABLE IF EXISTS benefit_daily_usage;

                DROP INDEX IF EXISTS ix_benefit_assignments_customer_valid;
                DROP INDEX IF EXISTS ix_benefit_assignments_customer_id;
                DROP INDEX IF EXISTS ix_benefit_assignments_benefit_definition_id;
                DROP TABLE IF EXISTS benefit_assignments;

                ALTER TABLE benefit_definitions DROP CONSTRAINT IF EXISTS fk_benefit_definitions_allowance_category;
                DROP INDEX IF EXISTS ix_benefit_definitions_allowance_category_id;
                DROP INDEX IF EXISTS ix_benefit_definitions_code;
                DROP TABLE IF EXISTS benefit_definitions;
            ");
        }
    }
}
