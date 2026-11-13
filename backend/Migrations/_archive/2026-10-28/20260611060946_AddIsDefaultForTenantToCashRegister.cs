using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultForTenantToCashRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_default_for_tenant",
                table: "cash_registers",
                nullable: false,
                defaultValue: false);

            // Set first cash register as default for each tenant
            migrationBuilder.Sql(@"
                UPDATE cash_registers 
                SET is_default_for_tenant = true 
                WHERE id IN (
                    SELECT DISTINCT ON (tenant_id) id 
                    FROM cash_registers 
                    ORDER BY tenant_id, created_at
                )
            ");

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_tenant_id",
                table: "cash_registers",
                column: "tenant_id",
                unique: true,
                filter: "\"is_default_for_tenant\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cash_registers_tenant_id",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "is_default_for_tenant",
                table: "cash_registers");
        }
    }
}
