using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717192000_AddOnlineOrderTrackingLoyalty")]
public partial class AddOnlineOrderTrackingLoyalty : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "customer_id",
            table: "online_orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_online_orders_customer_id",
            table: "online_orders",
            column: "customer_id");

        migrationBuilder.CreateTable(
            name: "online_order_status_changes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                online_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_online_order_status_changes", x => x.id);
                table.ForeignKey(
                    name: "FK_online_order_status_changes_online_orders_online_order_id",
                    column: x => x.online_order_id,
                    principalTable: "online_orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_online_order_status_changes_tenant_id",
            table: "online_order_status_changes",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_online_order_status_changes_order_id",
            table: "online_order_status_changes",
            column: "online_order_id");

        migrationBuilder.CreateIndex(
            name: "idx_online_order_status_changes_order_changed",
            table: "online_order_status_changes",
            columns: new[] { "online_order_id", "changed_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "online_order_status_changes");

        migrationBuilder.DropIndex(
            name: "idx_online_orders_customer_id",
            table: "online_orders");

        migrationBuilder.DropColumn(
            name: "customer_id",
            table: "online_orders");
    }
}
