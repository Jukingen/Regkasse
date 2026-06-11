using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cashier_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    cashier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    end_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_sales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_cash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_card = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashier_shifts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cashier_shifts_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cashier_shifts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_cash_register_id_started_at",
                table: "cashier_shifts",
                columns: new[] { "cash_register_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_tenant_id_cashier_id_status",
                table: "cashier_shifts",
                columns: new[] { "tenant_id", "cashier_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cashier_shifts");
        }
    }
}
