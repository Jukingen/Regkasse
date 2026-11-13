using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    variant_label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    price_delta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_variants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "beherbergung",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "doctor",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy-tables",
                column: "pos_features",
                value: "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "handy-shop",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "retail",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":true,\"lotTracking\":false,\"multiBranch\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "taxi",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "ticket-sales",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":true,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_tenant_id",
                table: "product_variants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_product_label",
                table: "product_variants",
                columns: new[] { "product_id", "variant_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_tenant_sku",
                table: "product_variants",
                columns: new[] { "tenant_id", "sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "beherbergung",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "doctor",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy-tables",
                column: "pos_features",
                value: "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "handy-shop",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "retail",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":false,\"lotTracking\":false,\"multiBranch\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "taxi",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "ticket-sales",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":true}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}");
        }
    }
}
