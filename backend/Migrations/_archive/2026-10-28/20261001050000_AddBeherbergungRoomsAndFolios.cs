using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBeherbergungRoomsAndFolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooms_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guest_folios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    check_out = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_folios", x => x.id);
                    table.ForeignKey(
                        name: "FK_guest_folios_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_folios_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_folios_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_tenant_id",
                table: "rooms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_tenant_number",
                table: "rooms",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_tenant_id",
                table: "guest_folios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_tenant_room",
                table: "guest_folios",
                columns: new[] { "tenant_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guest_folios_customer_id",
                table: "guest_folios",
                column: "customer_id");

            migrationBuilder.InsertData(
                table: "vertical_profiles",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "optional_fields", "pos_features", "pos_layout", "required_fields", "updated_at_utc" },
                values: new object[]
                {
                    "beherbergung",
                    new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc),
                    true,
                    "verticalProfiles.beherbergung.name",
                    "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}",
                    "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true}",
                    "standard",
                    "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\"]}",
                    new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc),
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "guest_folios");
            migrationBuilder.DropTable(name: "rooms");
            migrationBuilder.DeleteData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "beherbergung");
        }
    }
}
