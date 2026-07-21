using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AuthSessionTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "auth_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_tenant_id",
                table: "auth_sessions",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_auth_sessions_tenants_tenant_id",
                table: "auth_sessions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_auth_sessions_tenants_tenant_id",
                table: "auth_sessions");

            migrationBuilder.DropIndex(
                name: "IX_auth_sessions_tenant_id",
                table: "auth_sessions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "auth_sessions");
        }
    }
}
