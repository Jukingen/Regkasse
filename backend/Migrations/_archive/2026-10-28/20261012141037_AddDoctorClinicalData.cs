using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorClinicalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "pet_data",
                table: "customers",
                newName: "clinical_data");

            // Keep existing pet JSON bytes and add the generalized keys beside them.
            migrationBuilder.Sql(
                """
                UPDATE customers
                SET clinical_data = clinical_data || jsonb_strip_nulls(jsonb_build_object(
                    'Species', clinical_data->>'PetSpecies',
                    'Breed', clinical_data->>'PetBreed',
                    'BirthDate', clinical_data->>'PetBirthDate'))
                WHERE clinical_data IS NOT NULL
                  AND jsonb_typeof(clinical_data) = 'object';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "requires_prescription",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "optional_fields",
                value: "{\"customer\":[\"phone\",\"email\",\"petSpecies\",\"petBreed\",\"petBirthDate\",\"vaccines\",\"patientNotes\"],\"product\":[\"description\",\"category\"]}");

            migrationBuilder.InsertData(
                table: "vertical_profiles",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "optional_fields", "pos_features", "pos_layout", "required_fields", "updated_at_utc" },
                values: new object[] { "doctor", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), true, "verticalProfiles.doctor.name", "{\"customer\":[\"phone\",\"email\",\"birthDate\",\"allergies\",\"chronicConditions\",\"insurance\"],\"product\":[\"description\",\"category\"]}", "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false}", "appointment", "{\"customer\":[\"name\"],\"product\":[\"name\",\"price\",\"durationMinutes\"]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "doctor");

            migrationBuilder.DropColumn(
                name: "requires_prescription",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "clinical_data",
                table: "customers",
                newName: "pet_data");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "optional_fields",
                value: "{\"customer\":[\"phone\",\"email\",\"petSpecies\",\"petBreed\",\"petBirthDate\",\"patientNotes\"],\"product\":[\"description\",\"category\"]}");
        }
    }
}
