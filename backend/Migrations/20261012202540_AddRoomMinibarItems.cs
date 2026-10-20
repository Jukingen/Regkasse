using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomMinibarItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_minibar_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    charged_folio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    charged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_minibar_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_guest_folios_charged_folio_id",
                        column: x => x.charged_folio_id,
                        principalTable: "guest_folios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_minibar_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_charged_folio_id",
                table: "room_minibar_items",
                column: "charged_folio_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_product_id",
                table: "room_minibar_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_minibar_items_room_id",
                table: "room_minibar_items",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_minibar_items_tenant_room_status",
                table: "room_minibar_items",
                columns: new[] { "tenant_id", "room_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_room_minibar_items_tenant_id",
                table: "room_minibar_items",
                columns: new[] { "tenant_id", "id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_minibar_items");
        }
    }
}
