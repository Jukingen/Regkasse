using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryOptimizationCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_payment_details_cash_register_id_created_at",
                table: "payment_details",
                columns: new[] { "cash_register_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id_timestamp",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_name_timestamp",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_name", "timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_timestamp",
                table: "audit_logs",
                columns: new[] { "tenant_id", "timestamp" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_details_cash_register_id_created_at",
                table: "payment_details");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_entity_type_entity_id_timestamp",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_entity_type_entity_name_timestamp",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id_timestamp",
                table: "audit_logs");
        }
    }
}
