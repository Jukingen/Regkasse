using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLicenseSaleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_license_sale_id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_license_activation_utc",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "license_activation_count",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "idx_tenants_current_license_sale_id",
                table: "tenants",
                column: "current_license_sale_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_license_sales_current_license_sale_id",
                table: "tenants",
                column: "current_license_sale_id",
                principalTable: "license_sales",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_license_sales_current_license_sale_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "idx_tenants_current_license_sale_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "current_license_sale_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "last_license_activation_utc",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "license_activation_count",
                table: "tenants");
        }
    }
}
