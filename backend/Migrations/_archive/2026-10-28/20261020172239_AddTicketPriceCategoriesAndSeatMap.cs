using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPriceCategoriesAndSeatMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "ticket_seats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "grid_column",
                table: "ticket_seats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reserved_by_user_id",
                table: "ticket_seats",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seat_role",
                table: "ticket_seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "ticket_seats",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "stage_edge",
                table: "ticket_events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "top");

            migrationBuilder.CreateTable(
                name: "ticket_price_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_price_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_price_categories_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_event_category_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_event_category_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_event_category_prices_ticket_events_event_id",
                        column: x => x.event_id,
                        principalTable: "ticket_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_event_category_prices_ticket_price_categories_catego~",
                        column: x => x.category_id,
                        principalTable: "ticket_price_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_seats_category_id",
                table: "ticket_seats",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_event_category_prices_category_id",
                table: "ticket_event_category_prices",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_event_category_prices_event_id",
                table: "ticket_event_category_prices",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_event_category_prices_event_category",
                table: "ticket_event_category_prices",
                columns: new[] { "event_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_price_categories_tenant_id",
                table: "ticket_price_categories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_price_categories_tenant_code",
                table: "ticket_price_categories",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_seats_ticket_price_categories_category_id",
                table: "ticket_seats",
                column: "category_id",
                principalTable: "ticket_price_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_seats_ticket_price_categories_category_id",
                table: "ticket_seats");

            migrationBuilder.DropTable(
                name: "ticket_event_category_prices");

            migrationBuilder.DropTable(
                name: "ticket_price_categories");

            migrationBuilder.DropIndex(
                name: "ix_ticket_seats_category_id",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "grid_column",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "reserved_by_user_id",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "seat_role",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "ticket_seats");

            migrationBuilder.DropColumn(
                name: "stage_edge",
                table: "ticket_events");
        }
    }
}
