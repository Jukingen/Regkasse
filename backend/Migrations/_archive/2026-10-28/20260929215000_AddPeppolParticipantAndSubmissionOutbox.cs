using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPeppolParticipantAndSubmissionOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einvoice_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_einvoice_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_einvoice_submissions_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_einvoice_submissions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "peppol_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ap_environment = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peppol_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_peppol_participants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_correlation_id",
                table: "einvoice_submissions",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_invoice_id",
                table: "einvoice_submissions",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_einvoice_submissions_tenant_id",
                table: "einvoice_submissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_peppol_participants_tenant_id_participant_id",
                table: "peppol_participants",
                columns: new[] { "tenant_id", "participant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einvoice_submissions");

            migrationBuilder.DropTable(
                name: "peppol_participants");
        }
    }
}
