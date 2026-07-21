using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDeleteUserTenantMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_tenant_memberships_tenants_tenant_id",
                table: "user_tenant_memberships");

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenant_memberships_tenants_tenant_id",
                table: "user_tenant_memberships",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_tenant_memberships_tenants_tenant_id",
                table: "user_tenant_memberships");

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenant_memberships_tenants_tenant_id",
                table: "user_tenant_memberships",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
