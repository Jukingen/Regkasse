using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260812220000_AddSupportTickets")]
public partial class AddSupportTickets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "support_tickets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                created_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                assigned_to_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                assigned_to_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_tickets", x => x.id);
                table.ForeignKey(
                    name: "FK_support_tickets_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "support_ticket_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                author_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                author_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                is_staff_reply = table.Column<bool>(type: "boolean", nullable: false),
                is_internal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_ticket_messages", x => x.id);
                table.ForeignKey(
                    name: "FK_support_ticket_messages_support_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalTable: "support_tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_support_ticket_messages_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_support_tickets_ticket_number",
            table: "support_tickets",
            column: "ticket_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_support_tickets_tenant_id",
            table: "support_tickets",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_support_tickets_status",
            table: "support_tickets",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_support_tickets_created_at",
            table: "support_tickets",
            column: "created_at_utc");

        migrationBuilder.CreateIndex(
            name: "idx_support_tickets_status_created",
            table: "support_tickets",
            columns: new[] { "status", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "idx_support_ticket_messages_ticket_id",
            table: "support_ticket_messages",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "idx_support_ticket_messages_tenant_id",
            table: "support_ticket_messages",
            column: "tenant_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "support_ticket_messages");
        migrationBuilder.DropTable(name: "support_tickets");
    }
}
