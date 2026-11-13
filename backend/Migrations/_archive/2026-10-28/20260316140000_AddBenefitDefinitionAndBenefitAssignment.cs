using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Adds benefit_definitions and benefit_assignments tables for future customer benefit model.
    /// Idempotent; no change to payment, receipt, or daily closing flows.
    /// </summary>
    public partial class AddBenefitDefinitionAndBenefitAssignment : Migration
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
                    percentage_value decimal(5,2) NULL,
                    allowance_quantity integer NULL,
                    allowance_scope character varying(50) NULL,
                    buy_x_quantity integer NULL,
                    get_y_quantity integer NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NULL,
                    created_by character varying(450) NULL,
                    updated_by character varying(450) NULL,
                    is_active boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_benefit_definitions_code ON benefit_definitions (code);

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
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ix_benefit_assignments_customer_valid;
                DROP INDEX IF EXISTS ix_benefit_assignments_customer_id;
                DROP INDEX IF EXISTS ix_benefit_assignments_benefit_definition_id;
                DROP TABLE IF EXISTS benefit_assignments;
                DROP INDEX IF EXISTS ix_benefit_definitions_code;
                DROP TABLE IF EXISTS benefit_definitions;
            ");
        }
    }
}
