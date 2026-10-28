using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileServiceAddressAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_data",
                table: "customers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location_data",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "optional_fields",
                value: "{\"customer\":[\"email\",\"notes\",\"street\",\"postalCode\",\"city\"],\"product\":[\"description\",\"category\"],\"order\":[\"location\"]}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_data",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "location_data",
                table: "orders");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "optional_fields",
                value: "{\"customer\":[\"email\",\"notes\"],\"product\":[\"description\",\"category\"]}");
        }
    }
}
