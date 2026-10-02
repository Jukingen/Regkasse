using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomsAndGuestFolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "guest_folios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "guest_folios",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE guest_folios
                SET status = 1
                WHERE check_out IS NOT NULL AND check_out <= now();
                """);

            migrationBuilder.CreateTable(
                name: "guest_folio_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    folio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_folio_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_guest_folio_items_guest_folios_folio_id",
                        column: x => x.folio_id,
                        principalTable: "guest_folios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guest_folio_items_payment_details_payment_detail_id",
                        column: x => x.payment_detail_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guest_folio_items_folio_id",
                table: "guest_folio_items",
                column: "folio_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folio_items_payment_detail_id",
                table: "guest_folio_items",
                column: "payment_detail_id");

            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":true,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true}',
                    pos_layout = 'rooms',
                    updated_at_utc = now()
                WHERE id = 'beherbergung';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "guest_folio_items");
            migrationBuilder.DropColumn(name: "status", table: "rooms");
            migrationBuilder.DropColumn(name: "notes", table: "guest_folios");
            migrationBuilder.DropColumn(name: "status", table: "guest_folios");
            migrationBuilder.Sql(
                """
                UPDATE vertical_profiles
                SET pos_features = '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true}',
                    pos_layout = 'standard',
                    updated_at_utc = now()
                WHERE id = 'beherbergung';
                """);
        }
    }
}
