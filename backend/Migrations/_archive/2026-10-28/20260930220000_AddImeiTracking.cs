using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddImeiTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "imei_tracked",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "product_imeis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    sold_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warranty_months = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sold_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_imeis", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_imeis_payment_details_sold_payment_id",
                        column: x => x.sold_payment_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_imeis_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_imeis_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_products_imei_tracked",
                table: "products",
                column: "imei_tracked",
                filter: "imei_tracked = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_product_imeis_product_id",
                table: "product_imeis",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_imeis_sold_payment_id",
                table: "product_imeis",
                column: "sold_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_imeis_tenant_id",
                table: "product_imeis",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_imeis_tenant_product_status",
                table: "product_imeis",
                columns: new[] { "tenant_id", "product_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_product_imeis_tenant_imei",
                table: "product_imeis",
                columns: new[] { "tenant_id", "imei" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_imeis");

            migrationBuilder.DropIndex(
                name: "idx_products_imei_tracked",
                table: "products");

            migrationBuilder.DropColumn(
                name: "imei_tracked",
                table: "products");
        }
    }
}
