using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddSplitSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_carts_id",
                table: "carts",
                column: "id");

            migrationBuilder.CreateTable(
                name: "split_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_split_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_split_sessions_carts_original_cart_id",
                        column: x => x.original_cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_split_sessions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "split_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    split_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_cart_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seat_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_split_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_split_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_split_items_split_sessions_split_session_id",
                        column: x => x.split_session_id,
                        principalTable: "split_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_split_items_product_id",
                table: "split_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_items_split_session_id",
                table: "split_items",
                column: "split_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_items_split_session_id_seat_number",
                table: "split_items",
                columns: new[] { "split_session_id", "seat_number" });

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_original_cart_id",
                table: "split_sessions",
                column: "original_cart_id");

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_tenant_id_cashier_id_created_at",
                table: "split_sessions",
                columns: new[] { "tenant_id", "cashier_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_split_sessions_tenant_id_original_cart_id_is_completed",
                table: "split_sessions",
                columns: new[] { "tenant_id", "original_cart_id", "is_completed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "split_items");

            migrationBuilder.DropTable(
                name: "split_sessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_carts_id",
                table: "carts");
        }
    }
}
