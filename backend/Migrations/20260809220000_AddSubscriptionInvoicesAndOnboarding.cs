using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260809220000_AddSubscriptionInvoicesAndOnboarding")]
public partial class AddSubscriptionInvoicesAndOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "subscription_invoices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                license_type = table.Column<int>(type: "integer", nullable: false),
                amount_net = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                amount_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                amount_gross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_subscription_invoices", x => x.id);
                table.ForeignKey(
                    name: "FK_subscription_invoices_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_subscription_invoices_tenant_id",
            table: "subscription_invoices",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_subscription_invoices_period",
            table: "subscription_invoices",
            columns: new[] { "tenant_id", "period_start_utc", "period_end_utc" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_subscription_invoices_invoice_number",
            table: "subscription_invoices",
            column: "invoice_number",
            unique: true);

        migrationBuilder.CreateTable(
            name: "tenant_onboarding_status",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                step = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                is_completed = table.Column<bool>(type: "boolean", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_onboarding_status", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_onboarding_status_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenant_onboarding_status_tenant_step",
            table: "tenant_onboarding_status",
            columns: new[] { "tenant_id", "step" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "subscription_invoices");
        migrationBuilder.DropTable(name: "tenant_onboarding_status");
    }
}
