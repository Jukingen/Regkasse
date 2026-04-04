using System;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class Wave3ATenantScopedCategoriesAndProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultTenant = LegacyDefaultTenantIds.Primary;

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_barcode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_categories_Name",
                table: "categories");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "categories",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenant);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_categories_id_tenant_id",
                table: "categories",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id_tenant_id",
                table: "products",
                columns: new[] { "category_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL AND barcode <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories",
                columns: new[] { "tenant_id", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_categories_tenants_tenant_id",
                table: "categories",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id_tenant_id",
                table: "products",
                columns: new[] { "category_id", "tenant_id" },
                principalTable: "categories",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tenants_tenant_id",
                table: "products",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categories_tenants_tenant_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_category_id_tenant_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tenants_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_categories_id_tenant_id",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "IX_categories_tenant_id_Name",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
