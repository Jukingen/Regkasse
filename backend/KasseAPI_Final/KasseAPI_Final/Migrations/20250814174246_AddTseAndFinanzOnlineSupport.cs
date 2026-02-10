using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTseAndFinanzOnlineSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_CategoryId1",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CategoryId1",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryId1",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // CategoryId sütunu zaten mevcut, değiştirmeye gerek yok
            // migrationBuilder.AlterColumn<Guid>(
            //     name: "CategoryId",
            //     table: "products",
            //     type: "uuid",
            //     nullable: true,
            //     oldClrType: typeof(string),
            //     oldType: "text",
            //     oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "inventory_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTseDeviceId",
                table: "company_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanzOnlineApiUrl",
                table: "company_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinanzOnlineAutoSubmit",
                table: "company_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FinanzOnlineEnableValidation",
                table: "company_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FinanzOnlinePassword",
                table: "company_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinanzOnlineRetryAttempts",
                table: "company_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinanzOnlineSubmitInterval",
                table: "company_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FinanzOnlineUsername",
                table: "company_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TseAutoConnect",
                table: "company_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TseConnectionTimeout",
                table: "company_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TseDevices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VendorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    LastConnectionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSignatureTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CertificateStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MemoryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CanCreateInvoices = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    KassenId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FinanzOnlineUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FinanzOnlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFinanzOnlineSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PendingInvoices = table.Column<int>(type: "integer", nullable: false),
                    PendingReports = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseDevices", x => x.id);
                });
 
            // UserSettings tablosu zaten mevcut, oluşturmaya gerek yok

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId",
                table: "products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ApplicationUserId",
                table: "orders",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CustomerId",
                table: "invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_ApplicationUserId",
                table: "inventory_transactions",
                column: "ApplicationUserId");

            // UserSettings index'leri zaten mevcut, oluşturmaya gerek yok

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_transactions_AspNetUsers_ApplicationUserId",
                table: "inventory_transactions",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_customers_CustomerId",
                table: "invoices",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_AspNetUsers_ApplicationUserId",
                table: "orders",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            // CategoryId foreign key constraint eklenmeyecek
            // migrationBuilder.AddForeignKey(
            //     name: "FK_products_categories_CategoryId",
            //     table: "products",
            //     column: "CategoryId",
            //     principalTable: "categories",
            //     principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_transactions_AspNetUsers_ApplicationUserId",
                table: "inventory_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_customers_CustomerId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_AspNetUsers_ApplicationUserId",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_CategoryId",
                table: "products");

            migrationBuilder.DropTable(
                name: "TseDevices");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_products_CategoryId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_orders_ApplicationUserId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CustomerId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_inventory_transactions_ApplicationUserId",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "DefaultTseDeviceId",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineApiUrl",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineAutoSubmit",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineEnableValidation",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlinePassword",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineRetryAttempts",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineSubmitInterval",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineUsername",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "TseAutoConnect",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "TseConnectionTimeout",
                table: "company_settings");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                table: "products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            // CategoryId sütunu zaten mevcut, değiştirmeye gerek yok
            // migrationBuilder.AlterColumn<string>(
            //     name: "CategoryId",
            //     table: "products",
            //     type: "text",
            //     nullable: true,
            //     oldClrType: typeof(Guid),
            //     oldType: "uuid",
            //     oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId1",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId1",
                table: "products",
                column: "CategoryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_CategoryId1",
                table: "products",
                column: "CategoryId1",
                principalTable: "categories",
                principalColumn: "id");
        }
    }
}
