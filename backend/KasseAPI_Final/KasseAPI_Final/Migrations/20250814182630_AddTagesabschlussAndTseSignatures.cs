using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTagesabschlussAndTseSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MemoryStatus",
                table: "TseDevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            // migrationBuilder.AlterColumn<Guid>(
            //     name: "KassenId",
            //     table: "TseDevices",
            //     type: "uuid",
            //     maxLength: 50,
            //     nullable: false,
            //     oldClrType: typeof(string),
            //     oldType: "character varying(100)",
            //     oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceType",
                table: "TseDevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CertificateStatus",
                table: "TseDevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<Guid>(
                name: "CashRegisterId",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "FinanzOnlineEnabled",
                table: "company_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFinanzOnlineSync",
                table: "company_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingInvoices",
                table: "company_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    TseSignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinanzOnlineStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FinanzOnlineError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FinanzOnlineReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyClosings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyClosings_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanzOnlineErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanzOnlineErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanzOnlineErrors_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TseSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Signature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SignatureType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TseDeviceId = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TseSignatures_TseDevices_TseDeviceId",
                        column: x => x.TseDeviceId,
                        principalTable: "TseDevices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TseSignatures_cash_registers_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TseDevices_SerialNumber",
                table: "TseDevices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_CashRegisterId_ClosingDate_ClosingType",
                table: "DailyClosings",
                columns: new[] { "CashRegisterId", "ClosingDate", "ClosingType" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_UserId",
                table: "DailyClosings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_CashRegisterId",
                table: "FinanzOnlineErrors",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_ErrorType_OccurredAt",
                table: "FinanzOnlineErrors",
                columns: new[] { "ErrorType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanzOnlineErrors_ReferenceId",
                table: "FinanzOnlineErrors",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_CashRegisterId_CreatedAt",
                table: "TseSignatures",
                columns: new[] { "CashRegisterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_Signature",
                table: "TseSignatures",
                column: "Signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TseSignatures_TseDeviceId",
                table: "TseSignatures",
                column: "TseDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyClosings");

            migrationBuilder.DropTable(
                name: "FinanzOnlineErrors");

            migrationBuilder.DropTable(
                name: "TseSignatures");

            migrationBuilder.DropIndex(
                name: "IX_TseDevices_SerialNumber",
                table: "TseDevices");

            migrationBuilder.DropColumn(
                name: "CashRegisterId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "FinanzOnlineEnabled",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "LastFinanzOnlineSync",
                table: "company_settings");

            migrationBuilder.DropColumn(
                name: "PendingInvoices",
                table: "company_settings");

            migrationBuilder.AlterColumn<string>(
                name: "MemoryStatus",
                table: "TseDevices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            // migrationBuilder.AlterColumn<string>(
            //     name: "KassenId",
            //     table: "TseDevices",
            //     type: "character varying(100)",
            //     maxLength: 100,
            //     nullable: false,
            //     oldClrType: typeof(Guid),
            //     oldType: "uuid",
            //     oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceType",
                table: "TseDevices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "CertificateStatus",
                table: "TseDevices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
