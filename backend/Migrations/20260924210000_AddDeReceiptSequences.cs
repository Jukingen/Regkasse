using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260924210000_AddDeReceiptSequences")]
public partial class AddDeReceiptSequences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "de_receipt_sequences",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                next_sequence = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_de_receipt_sequences", x => x.id);
                table.ForeignKey(
                    name: "FK_de_receipt_sequences_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_de_receipt_sequences_cash_registers_cash_register_id",
                    column: x => x.cash_register_id,
                    principalTable: "cash_registers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_de_receipt_sequences_cash_register_id",
            table: "de_receipt_sequences",
            column: "cash_register_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_de_receipt_sequences_tenant_id",
            table: "de_receipt_sequences",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "de_receipt_sequences");
    }
}
