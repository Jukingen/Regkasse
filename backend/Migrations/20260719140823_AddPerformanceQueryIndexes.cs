using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Additive query indexes only (non-breaking). Does not alter columns or business logic.
    /// Snapshot/Designer may also reflect other pending model entities already introduced by
    /// earlier hand-written migrations in this branch; Up/Down intentionally create/drop indexes only.
    /// </summary>
    public partial class AddPerformanceQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_products_tenant_name",
                table: "products",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_tenant_status",
                table: "offline_orders",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_daily_closings_tenant_register_date",
                table: "DailyClosings",
                columns: new[] { "tenant_id", "CashRegisterId", "ClosingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_products_tenant_name",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_offline_orders_tenant_status",
                table: "offline_orders");

            migrationBuilder.DropIndex(
                name: "idx_daily_closings_tenant_register_date",
                table: "DailyClosings");
        }
    }
}
