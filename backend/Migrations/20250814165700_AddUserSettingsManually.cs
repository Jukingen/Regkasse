using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingsManually : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeFormat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CashRegisterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultTaxRate = table.Column<int>(type: "integer", nullable: false),
                    EnableDiscounts = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCoupons = table.Column<bool>(type: "boolean", nullable: false),
                    AutoPrintReceipts = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptHeader = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptFooter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TseDeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FinanzOnlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FinanzOnlineUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SessionTimeout = table.Column<int>(type: "integer", nullable: false),
                    RequirePinForRefunds = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercentage = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompactMode = table.Column<bool>(type: "boolean", nullable: false),
                    ShowProductImages = table.Column<bool>(type: "boolean", nullable: false),
                    EnableNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    LowStockAlert = table.Column<bool>(type: "boolean", nullable: false),
                    DailyReportEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DefaultPaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultTableNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DefaultWaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Currency",
                table: "UserSettings",
                column: "Currency");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Language",
                table: "UserSettings",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
