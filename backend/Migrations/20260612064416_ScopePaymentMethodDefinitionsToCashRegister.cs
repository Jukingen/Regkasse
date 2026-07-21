using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class ScopePaymentMethodDefinitionsToCashRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_is_active_display_order",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_tenant_id_code",
                table: "payment_method_definitions");

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "payment_method_definitions",
                type: "uuid",
                nullable: true);

            // Duplicate existing tenant-level rows for every cash register in the same tenant.
            migrationBuilder.Sql(@"
                INSERT INTO payment_method_definitions (
                    id, tenant_id, cash_register_id, code, name, is_active, is_default, display_order,
                    legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                    allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
                )
                SELECT
                    gen_random_uuid(),
                    pmd.tenant_id,
                    cr.id,
                    pmd.code,
                    pmd.name,
                    pmd.is_active,
                    pmd.is_default,
                    pmd.display_order,
                    pmd.legacy_payment_method_value,
                    pmd.fiscal_category,
                    pmd.requires_terminal,
                    pmd.terminal_type,
                    pmd.allow_refund,
                    pmd.icon,
                    pmd.metadata_json,
                    pmd.created_at_utc,
                    NOW() AT TIME ZONE 'utc'
                FROM payment_method_definitions pmd
                INNER JOIN cash_registers cr ON cr.tenant_id = pmd.tenant_id
                WHERE pmd.cash_register_id IS NULL;
            ");

            migrationBuilder.Sql(@"
                DELETE FROM payment_method_definitions WHERE cash_register_id IS NULL;
            ");

            // Registers without any catalog rows get the standard default set.
            migrationBuilder.Sql(@"
                INSERT INTO payment_method_definitions (
                    id, tenant_id, cash_register_id, code, name, is_active, is_default, display_order,
                    legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type,
                    allow_refund, icon, metadata_json, created_at_utc, updated_at_utc
                )
                SELECT gen_random_uuid(), cr.tenant_id, cr.id, v.code, v.name, v.is_active, v.is_default, v.display_order,
                    v.legacy_payment_method_value, v.fiscal_category, v.requires_terminal, v.terminal_type,
                    TRUE, v.icon, NULL, NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc'
                FROM cash_registers cr
                CROSS JOIN (
                    VALUES
                        ('cash', 'Bar', TRUE, TRUE, 10, 0, 'Cash', FALSE, NULL::varchar, 'cash-outline'),
                        ('card', 'Karte', TRUE, FALSE, 20, 1, 'Card', TRUE, 'card', 'card-outline'),
                        ('transfer', 'Überweisung', TRUE, FALSE, 30, 2, 'BankTransfer', FALSE, NULL::varchar, 'swap-horizontal-outline'),
                        ('voucher', 'Gutschein', TRUE, FALSE, 40, 4, 'Voucher', FALSE, NULL::varchar, 'ticket-outline'),
                        ('check', 'Scheck', FALSE, FALSE, 50, 3, 'Check', FALSE, NULL::varchar, NULL::varchar),
                        ('mobile', 'Mobil', FALSE, FALSE, 60, 5, 'Mobile', TRUE, 'softpos', NULL::varchar)
                ) AS v(code, name, is_active, is_default, display_order, legacy_payment_method_value, fiscal_category, requires_terminal, terminal_type, icon)
                WHERE NOT EXISTS (
                    SELECT 1 FROM payment_method_definitions pmd WHERE pmd.cash_register_id = cr.id
                );
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "cash_register_id",
                table: "payment_method_definitions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_cash_register_id_code",
                table: "payment_method_definitions",
                columns: new[] { "cash_register_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_tenant_id_cash_register_id_is_ac~",
                table: "payment_method_definitions",
                columns: new[] { "tenant_id", "cash_register_id", "is_active", "display_order" });

            migrationBuilder.AddForeignKey(
                name: "FK_payment_method_definitions_cash_registers_cash_register_id",
                table: "payment_method_definitions",
                column: "cash_register_id",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_method_definitions_cash_registers_cash_register_id",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_cash_register_id_code",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_tenant_id_cash_register_id_is_ac~",
                table: "payment_method_definitions");

            // Collapse back to one row per tenant+code (keep lowest display_order per tenant).
            migrationBuilder.Sql(@"
                DELETE FROM payment_method_definitions pmd
                WHERE pmd.id NOT IN (
                    SELECT DISTINCT ON (tenant_id, code) id
                    FROM payment_method_definitions
                    ORDER BY tenant_id, code, display_order, created_at_utc
                );
            ");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "payment_method_definitions");

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_is_active_display_order",
                table: "payment_method_definitions",
                columns: new[] { "is_active", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_tenant_id_code",
                table: "payment_method_definitions",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }
    }
}
