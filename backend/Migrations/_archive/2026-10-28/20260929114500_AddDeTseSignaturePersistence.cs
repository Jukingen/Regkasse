using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDeTseSignaturePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "einvoice_validation_passed",
                table: "invoices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "einvoice_validation_rule_ids",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "de_tse_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_details_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tss_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signature = table.Column<string>(type: "text", nullable: false),
                    signature_algorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    signed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    certificate_serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_de_tse_signatures", x => x.id);
                    table.ForeignKey(
                        name: "FK_de_tse_signatures_payment_details_payment_details_id",
                        column: x => x.payment_details_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_de_tse_signatures_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_de_tse_signatures_payment_details_id",
                table: "de_tse_signatures",
                column: "payment_details_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_de_tse_signatures_tenant_id",
                table: "de_tse_signatures",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "de_tse_signatures");

            migrationBuilder.DropColumn(
                name: "einvoice_validation_passed",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "einvoice_validation_rule_ids",
                table: "invoices");
        }
    }
}
