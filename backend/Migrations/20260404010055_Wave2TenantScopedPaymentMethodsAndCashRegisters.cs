using System;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class Wave2TenantScopedPaymentMethodsAndCashRegisters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_code",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_RegisterNumber",
                table: "cash_registers");

            // Default to legacy primary tenant so existing single-tenant rows satisfy FK and composite uniques.
            var defaultTenant = LegacyDefaultTenantIds.Primary;
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "payment_method_definitions",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "cash_registers",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_tenant_id_code",
                table: "payment_method_definitions",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_tenant_id_RegisterNumber",
                table: "cash_registers",
                columns: new[] { "tenant_id", "RegisterNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_tenants_tenant_id",
                table: "cash_registers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_method_definitions_tenants_tenant_id",
                table: "payment_method_definitions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_tenants_tenant_id",
                table: "cash_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_method_definitions_tenants_tenant_id",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_payment_method_definitions_tenant_id_code",
                table: "payment_method_definitions");

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_tenant_id_RegisterNumber",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "payment_method_definitions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "cash_registers");

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_definitions_code",
                table: "payment_method_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_RegisterNumber",
                table: "cash_registers",
                column: "RegisterNumber",
                unique: true);
        }
    }
}
