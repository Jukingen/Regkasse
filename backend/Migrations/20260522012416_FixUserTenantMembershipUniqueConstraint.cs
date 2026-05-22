using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// user_tenant_memberships: allow multiple tenants per user; unique on (user_id, tenant_id) only.
    /// </summary>
    public partial class FixUserTenantMembershipUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_tenant_memberships_user_id",
                table: "user_tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_user_tenant_memberships_user_id_tenant_id",
                table: "user_tenant_memberships");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id",
                table: "user_tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id_tenant_id",
                table: "user_tenant_memberships",
                columns: new[] { "user_id", "tenant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_tenant_memberships_user_id_tenant_id",
                table: "user_tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_user_tenant_memberships_user_id",
                table: "user_tenant_memberships");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id",
                table: "user_tenant_memberships",
                column: "user_id",
                unique: true,
                filter: "\"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_user_id_tenant_id",
                table: "user_tenant_memberships",
                columns: new[] { "user_id", "tenant_id" },
                unique: true);
        }
    }
}
