using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddMonatsbelegTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monatsbeleg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    TotalCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCard = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalVoucher = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalOther = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_20 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_10 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate_0 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    DailyClosingCount = table.Column<int>(type: "integer", nullable: false),
                    TseSignature = table.Column<string>(type: "text", nullable: true),
                    tse_signature_timestamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tse_certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousSignature = table.Column<string>(type: "text", nullable: true),
                    SignatureChainLength = table.Column<int>(type: "integer", nullable: false),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    daily_closing_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monatsbeleg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monatsbeleg_DailyClosings_daily_closing_id",
                        column: x => x.daily_closing_id,
                        principalTable: "DailyClosings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_monatsbeleg_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_monatsbeleg_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_monatsbeleg_daily_closing_id",
                table: "monatsbeleg",
                column: "daily_closing_id");

            migrationBuilder.CreateIndex(
                name: "ix_monatsbeleg_per_register_month",
                table: "monatsbeleg",
                columns: new[] { "cash_register_id", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monatsbeleg_tenant_id",
                table: "monatsbeleg",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monatsbeleg");
        }
    }
}
