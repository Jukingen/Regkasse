using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBarcodeScan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "beherbergung",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "doctor",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy-tables",
                column: "pos_features",
                value: "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "handy-shop",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "retail",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":true,\"lotTracking\":false,\"multiBranch\":false,\"barcodeScan\":true}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "taxi",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "ticket-sales",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":true,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false,\"barcodeScan\":false}");

            migrationBuilder.Sql(
                "UPDATE products SET barcode = NULL WHERE barcode IS NOT NULL AND btrim(barcode) = '';");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products");

            migrationBuilder.Sql(
                "UPDATE products SET barcode = '' WHERE barcode IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "beherbergung",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":true,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "doctor",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"prescription\":true,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "gastronomy-tables",
                column: "pos_features",
                value: "{\"tables\":true,\"kitchenDisplay\":true,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "hair-salon",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "handy-shop",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":true,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "mobile-services",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":true,\"appointment\":true,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "retail",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":false,\"variants\":true,\"lotTracking\":false,\"multiBranch\":false,\"barcodeScan\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "taxi",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":true,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "ticket-sales",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":false,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"ticketScan\":true,\"variants\":false}");

            migrationBuilder.UpdateData(
                table: "vertical_profiles",
                keyColumn: "id",
                keyValue: "vet",
                column: "pos_features",
                value: "{\"tables\":false,\"kitchenDisplay\":false,\"patientRecord\":true,\"serviceDuration\":false,\"appointment\":false,\"imeiTracking\":false,\"routeTracking\":false,\"roomTracking\":false,\"variants\":false}");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL AND barcode <> ''");
        }
    }
}
