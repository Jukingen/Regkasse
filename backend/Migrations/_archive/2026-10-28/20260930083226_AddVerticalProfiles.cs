using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddVerticalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "vertical_profile_id",
                table: "company_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_vertical_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overrides_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_vertical_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_vertical_overrides_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vertical_profiles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pos_features = table.Column<string>(type: "jsonb", nullable: false),
                    required_fields = table.Column<string>(type: "jsonb", nullable: false),
                    optional_fields = table.Column<string>(type: "jsonb", nullable: false),
                    pos_layout = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vertical_profiles", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "vertical_profiles",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "optional_fields", "pos_features", "pos_layout", "required_fields", "updated_at_utc" },
                values: new object[,]
                {
                    { "gastronomy", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.gastronomy.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\"]}", "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}", "standard", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "gastronomy-tables", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.gastronomyTables.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\"]}", "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}", "tables", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "hair-salon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.hairSalon.name", "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}", "appointment", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"serviceDuration\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "handy-shop", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.handyShop.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"stock\",\"imei\",\"serialNumber\",\"brand\",\"model\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false}", "standard", "{\"customer\":[],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "mobile-services", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.mobileServices.name", "{\"customer\":[\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false}", "appointment", "{\"customer\":[\"name\",\"phone\",\"address\"],\"product\":[\"name\",\"price\",\"serviceDuration\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "taxi", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.taxi.name", "{\"customer\":[\"name\",\"phone\",\"pickupAddress\",\"destinationAddress\"],\"product\":[\"description\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false}", "queue", "{\"customer\":[],\"product\":[\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "ticket-sales", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.ticketSales.name", "{\"customer\":[\"name\",\"phone\",\"email\"],\"product\":[\"description\",\"category\",\"room\",\"seat\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true}", "queue", "{\"customer\":[],\"product\":[\"name\",\"price\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.Sql(
                """
                UPDATE company_settings
                SET vertical_profile_id = 'gastronomy'
                WHERE country = 'AT' AND vertical_profile_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "idx_company_settings_vertical_profile_id",
                table: "company_settings",
                column: "vertical_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_vertical_overrides_tenant_id",
                table: "tenant_vertical_overrides",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vertical_profiles_is_active",
                table: "vertical_profiles",
                column: "is_active");

            migrationBuilder.AddForeignKey(
                name: "FK_company_settings_vertical_profiles_vertical_profile_id",
                table: "company_settings",
                column: "vertical_profile_id",
                principalTable: "vertical_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_settings_vertical_profiles_vertical_profile_id",
                table: "company_settings");

            migrationBuilder.DropTable(
                name: "tenant_vertical_overrides");

            migrationBuilder.DropTable(
                name: "vertical_profiles");

            migrationBuilder.DropIndex(
                name: "idx_company_settings_vertical_profile_id",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "vertical_profile_id",
                table: "company_settings");
        }
    }
}
