using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    license_plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    custom_valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price_net = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 20.00m),
                    vat_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    price_gross = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    sold_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sold_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_sales", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_sales_AspNetUsers_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_license_sales_AspNetUsers_sold_by_user_id",
                        column: x => x.sold_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_sales_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_invoice_number",
                table: "license_sales",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_license_key",
                table: "license_sales",
                column: "license_key");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_sold_at",
                table: "license_sales",
                column: "sold_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_license_sales_tenant_id",
                table: "license_sales",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_sales_cancelled_by_user_id",
                table: "license_sales",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_sales_sold_by_user_id",
                table: "license_sales",
                column: "sold_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_sales");
        }
    }
}
