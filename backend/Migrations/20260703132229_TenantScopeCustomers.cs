using System;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopeCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_customer_number",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_email",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tax_number",
                table: "customers");

            // Backfill existing (global) customer rows, including the walk-in guest, to the seeded default tenant.
            // Matches the Wave2/Wave3 tenant-scoping precedent (LegacyDefaultTenantIds.Primary).
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "customers",
                type: "uuid",
                nullable: false,
                defaultValue: LegacyDefaultTenantIds.Primary);

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id",
                table: "customers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_customer_number",
                table: "customers",
                columns: new[] { "tenant_id", "customer_number" },
                unique: true,
                filter: "customer_number <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_email",
                table: "customers",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "email <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_tax_number",
                table: "customers",
                columns: new[] { "tenant_id", "tax_number" },
                unique: true,
                filter: "tax_number <> ''");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tenant_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tenant_id_customer_number",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tenant_id_email",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_tenant_id_tax_number",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customer_number",
                table: "customers",
                column: "customer_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_email",
                table: "customers",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_tax_number",
                table: "customers",
                column: "tax_number",
                unique: true);
        }
    }
}
