using System;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Tenancy;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantsAndSettingsTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_settings_CompanyTaxNumber",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_CompanyRegistrationNumber",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_CompanyTaxNumber",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_CompanyVatNumber",
                table: "company_settings");

            var defaultTenantId = LegacyDefaultTenantIds.Primary;
            var seededAt = new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "id", "Name", "Slug", "created_at", "is_active" },
                values: new object[] { defaultTenantId, "Default", LegacyDefaultTenantIds.PrimarySlug, seededAt, true });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "system_settings",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "localization_settings",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "company_settings",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_tenant_id",
                table: "system_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_tenant_id_CompanyTaxNumber",
                table: "system_settings",
                columns: new[] { "tenant_id", "CompanyTaxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_localization_settings_tenant_id",
                table: "localization_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id",
                table: "company_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyRegistrationNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyRegistrationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyTaxNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyTaxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_tenant_id_CompanyVatNumber",
                table: "company_settings",
                columns: new[] { "tenant_id", "CompanyVatNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_company_settings_tenants_tenant_id",
                table: "company_settings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_localization_settings_tenants_tenant_id",
                table: "localization_settings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_system_settings_tenants_tenant_id",
                table: "system_settings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_settings_tenants_tenant_id",
                table: "company_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_localization_settings_tenants_tenant_id",
                table: "localization_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_system_settings_tenants_tenant_id",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_system_settings_tenant_id",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_system_settings_tenant_id_CompanyTaxNumber",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_localization_settings_tenant_id",
                table: "localization_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_tenant_id",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_tenant_id_CompanyRegistrationNumber",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_tenant_id_CompanyTaxNumber",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_company_settings_tenant_id_CompanyVatNumber",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "localization_settings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "company_settings");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Slug",
                table: "tenants");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_CompanyTaxNumber",
                table: "system_settings",
                column: "CompanyTaxNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_CompanyRegistrationNumber",
                table: "company_settings",
                column: "CompanyRegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_CompanyTaxNumber",
                table: "company_settings",
                column: "CompanyTaxNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_CompanyVatNumber",
                table: "company_settings",
                column: "CompanyVatNumber",
                unique: true);
        }
    }
}
