using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineOrdersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offline_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offline_order_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_data = table.Column<string>(type: "jsonb", nullable: false),
                    order_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    synced_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    synced_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sync_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_sync_attempt_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_offline_orders_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offline_orders_payment_details_synced_payment_id",
                        column: x => x.synced_payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_offline_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_created",
                table: "offline_orders",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_expires",
                table: "offline_orders",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_status",
                table: "offline_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_offline_orders_tenant",
                table: "offline_orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_orders_cash_register_id",
                table: "offline_orders",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_offline_orders_synced_payment_id",
                table: "offline_orders",
                column: "synced_payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offline_orders");
        }
    }
}
