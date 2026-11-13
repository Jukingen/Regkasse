using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailVerticalProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO vertical_profiles (
                    id, created_at_utc, is_active, name, optional_fields, pos_features, pos_layout, required_fields, updated_at_utc)
                SELECT
                    'retail',
                    TIMESTAMPTZ '2026-09-30 00:00:00+00',
                    TRUE,
                    'verticalProfiles.retail.name',
                    '{"customer":["email","phone"],"product":["category","stock"]}'::jsonb,
                    '{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false,"ticketScan":false,"variants":false,"lotTracking":false,"multiBranch":false,"barcodeScan":false}'::jsonb,
                    'standard',
                    '{"customer":["name"],"product":["sku","name","price"]}'::jsonb,
                    TIMESTAMPTZ '2026-09-30 00:00:00+00'
                WHERE NOT EXISTS (
                    SELECT 1 FROM vertical_profiles WHERE id = 'retail');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "retail");
        }
    }
}
