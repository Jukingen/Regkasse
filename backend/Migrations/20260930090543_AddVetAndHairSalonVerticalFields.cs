using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddVetAndHairSalonVerticalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "staff_id",
                table: "products",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pet_data",
                table: "customers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                columns: new[] { "optional_fields", "required_fields" },
                values: new object[] { "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\",\"staffId\"]}", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"durationMinutes\"]}" });

            migrationBuilder.InsertData(
                table: "vertical_profiles",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "optional_fields", "pos_features", "pos_layout", "required_fields", "updated_at_utc" },
                values: new object[] { "vet", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.vet.name", "{\"customer\":[\"phone\",\"email\",\"petSpecies\",\"petBreed\",\"petBirthDate\",\"patientNotes\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false}", "standard", "{\"customer\":[\"name\",\"petName\"],\"product\":[\"name\",\"price\",\"taxGroup\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "idx_products_tenant_staff_id",
                table: "products",
                columns: new[] { "tenant_id", "staff_id" },
                filter: "staff_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_duration_minutes_range",
                table: "products",
                sql: "duration_minutes IS NULL OR (duration_minutes >= 1 AND duration_minutes <= 1440)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_products_tenant_staff_id",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_duration_minutes_range",
                table: "products");

            migrationBuilder.Sql(
                """
                UPDATE company_settings
                SET vertical_profile_id = 'gastronomy'
                WHERE vertical_profile_id = 'vet';
                """);

            migrationBuilder.DeleteData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet");

            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "products");

            migrationBuilder.DropColumn(
                name: "staff_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "pet_data",
                table: "customers");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                columns: new[] { "optional_fields", "required_fields" },
                values: new object[] { "{\"customer\":[\"phone\",\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"serviceDuration\"]}" });
        }
    }
}
