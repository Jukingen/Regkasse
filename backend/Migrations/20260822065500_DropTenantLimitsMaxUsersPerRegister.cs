using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260822065500_DropTenantLimitsMaxUsersPerRegister")]
    public partial class DropTenantLimitsMaxUsersPerRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_users_per_register",
                table: "tenant_limits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_users_per_register",
                table: "tenant_limits",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }
    }
}
