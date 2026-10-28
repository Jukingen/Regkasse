using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ticket_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ticket_code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_payment_details_payment_detail_id",
                        column: x => x.payment_detail_id,
                        principalTable: "payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ticket_redemptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_payment_detail_id",
                table: "ticket_redemptions",
                column: "payment_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_tenant_id",
                table: "ticket_redemptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_redemptions_ticket_code",
                table: "ticket_redemptions",
                column: "ticket_code");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_redemptions_tenant_code_hash",
                table: "ticket_redemptions",
                columns: new[] { "tenant_id", "ticket_code_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_redemptions");
        }
    }
}
