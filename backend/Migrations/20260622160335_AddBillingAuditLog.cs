using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    price_net = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    price_gross = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    license_plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_audit_log_AspNetUsers_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billing_audit_log_license_sales_license_sale_id",
                        column: x => x.license_sale_id,
                        principalTable: "license_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billing_audit_log_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_created_at",
                table: "billing_audit_log",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_event_type",
                table: "billing_audit_log",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_license_sale_id",
                table: "billing_audit_log",
                column: "license_sale_id");

            migrationBuilder.CreateIndex(
                name: "idx_billing_audit_log_tenant_id",
                table: "billing_audit_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_audit_log_actor_user_id",
                table: "billing_audit_log",
                column: "actor_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_audit_log");
        }
    }
}
