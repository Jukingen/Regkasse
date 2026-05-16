using System;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToFiscalAndAuditTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var legacyTenant = LegacyDefaultTenantIds.Primary;

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "TseSignatures",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "signature_chain_state",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "receipts",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "receipt_tax_lines",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "receipt_items",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "offline_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "FinanzOnlineSubmissions",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "DailyClosings",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: legacyTenant);

            migrationBuilder.Sql(
                """
                UPDATE invoices i
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE i."CashRegisterId" = cr.id;

                UPDATE offline_transactions ot
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE ot."CashRegisterId" = cr.id;

                UPDATE "DailyClosings" dc
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE dc."CashRegisterId" = cr.id;

                UPDATE "TseSignatures" ts
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE ts."CashRegisterId" = cr.id;

                UPDATE signature_chain_state sc
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE sc.cash_register_id = cr.id;

                UPDATE receipts r
                SET tenant_id = cr.tenant_id
                FROM cash_registers cr
                WHERE r.cash_register_id = cr.id;

                UPDATE receipt_items ri
                SET tenant_id = r.tenant_id
                FROM receipts r
                WHERE ri.receipt_id = r.receipt_id;

                UPDATE receipt_tax_lines rtl
                SET tenant_id = r.tenant_id
                FROM receipts r
                WHERE rtl.receipt_id = r.receipt_id;

                UPDATE "FinanzOnlineSubmissions" fos
                SET tenant_id = i.tenant_id
                FROM invoices i
                WHERE fos."InvoiceId" = i.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_tenant_id",
                table: "TseSignatures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_tenant_id",
                table: "signature_chain_state",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_tenant_id",
                table: "receipts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_tax_lines_tenant_id",
                table: "receipt_tax_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_tenant_id",
                table: "receipt_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_transactions_tenant_id",
                table: "offline_transactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id",
                table: "invoices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineSubmissions_InvoiceId",
                table: "FinanzOnlineSubmissions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineSubmissions_tenant_id",
                table: "FinanzOnlineSubmissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_tenant_id",
                table: "DailyClosings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id",
                table: "audit_logs",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_tenant_id",
                table: "audit_logs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyClosings_tenants_tenant_id",
                table: "DailyClosings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanzOnlineSubmissions_tenants_tenant_id",
                table: "FinanzOnlineSubmissions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_tenants_tenant_id",
                table: "invoices",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offline_transactions_tenants_tenant_id",
                table: "offline_transactions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_items_tenants_tenant_id",
                table: "receipt_items",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_tax_lines_tenants_tenant_id",
                table: "receipt_tax_lines",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipts_tenants_tenant_id",
                table: "receipts",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_signature_chain_state_tenants_tenant_id",
                table: "signature_chain_state",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TseSignatures_tenants_tenant_id",
                table: "TseSignatures",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_tenants_tenant_id",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyClosings_tenants_tenant_id",
                table: "DailyClosings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanzOnlineSubmissions_tenants_tenant_id",
                table: "FinanzOnlineSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_tenants_tenant_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_offline_transactions_tenants_tenant_id",
                table: "offline_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_receipt_items_tenants_tenant_id",
                table: "receipt_items");

            migrationBuilder.DropForeignKey(
                name: "FK_receipt_tax_lines_tenants_tenant_id",
                table: "receipt_tax_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_receipts_tenants_tenant_id",
                table: "receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_signature_chain_state_tenants_tenant_id",
                table: "signature_chain_state");

            migrationBuilder.DropForeignKey(
                name: "FK_TseSignatures_tenants_tenant_id",
                table: "TseSignatures");

            migrationBuilder.DropIndex(
                name: "IX_TseSignatures_tenant_id",
                table: "TseSignatures");

            migrationBuilder.DropIndex(
                name: "IX_signature_chain_state_tenant_id",
                table: "signature_chain_state");

            migrationBuilder.DropIndex(
                name: "IX_receipts_tenant_id",
                table: "receipts");

            migrationBuilder.DropIndex(
                name: "IX_receipt_tax_lines_tenant_id",
                table: "receipt_tax_lines");

            migrationBuilder.DropIndex(
                name: "IX_receipt_items_tenant_id",
                table: "receipt_items");

            migrationBuilder.DropIndex(
                name: "IX_offline_transactions_tenant_id",
                table: "offline_transactions");

            migrationBuilder.DropIndex(
                name: "IX_invoices_tenant_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_FinanzOnlineSubmissions_InvoiceId",
                table: "FinanzOnlineSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FinanzOnlineSubmissions_tenant_id",
                table: "FinanzOnlineSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_tenant_id",
                table: "DailyClosings");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "TseSignatures");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "signature_chain_state");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "receipt_tax_lines");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "receipt_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "offline_transactions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "FinanzOnlineSubmissions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "audit_logs");
        }
    }
}
