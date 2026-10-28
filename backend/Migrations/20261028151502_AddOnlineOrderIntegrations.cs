using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineOrderIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "online_order_integration_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_location_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    claiming_user_id = table.Column<string>(type: "text", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    delivery_fee_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_fee_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_integration_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_integration_locations_AspNetUsers_claiming_use~",
                        column: x => x.claiming_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_online_order_integration_locations_products_delivery_fee_pr~",
                        column: x => x.delivery_fee_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_online_order_integration_locations_products_service_fee_pro~",
                        column: x => x.service_fee_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_online_order_integration_locations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "online_order_integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    external_location_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    online_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "received"),
                    raw_payload = table.Column<string>(type: "text", nullable: true),
                    payload_sha256 = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_integrations", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_integrations_online_orders_online_order_id",
                        column: x => x.online_order_id,
                        principalTable: "online_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_online_order_integrations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integration_locations_claiming_user",
                table: "online_order_integration_locations",
                column: "claiming_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integration_locations_delivery_product",
                table: "online_order_integration_locations",
                column: "delivery_fee_product_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integration_locations_service_product",
                table: "online_order_integration_locations",
                column: "service_fee_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_online_order_integration_locations_tenant_id",
                table: "online_order_integration_locations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_online_order_integration_locations_provider_location",
                table: "online_order_integration_locations",
                columns: new[] { "provider", "external_location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integrations_online_order_id",
                table: "online_order_integrations",
                column: "online_order_id");

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integrations_status_next_attempt",
                table: "online_order_integrations",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "idx_online_order_integrations_tenant_status_received",
                table: "online_order_integrations",
                columns: new[] { "tenant_id", "status", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ux_online_order_integrations_tenant_provider_external",
                table: "online_order_integrations",
                columns: new[] { "tenant_id", "provider", "external_order_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "online_order_integration_locations");

            migrationBuilder.DropTable(
                name: "online_order_integrations");
        }
    }
}
