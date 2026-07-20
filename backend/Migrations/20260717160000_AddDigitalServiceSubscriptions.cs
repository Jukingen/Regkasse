using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717160000_AddDigitalServiceSubscriptions")]
public partial class AddDigitalServiceSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "digital_service_subscriptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_billing_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                cancelled_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_digital_service_subscriptions", x => x.id);
                table.ForeignKey(
                    name: "FK_digital_service_subscriptions_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_subscriptions_tenant_id",
            table: "digital_service_subscriptions",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_subscriptions_status",
            table: "digital_service_subscriptions",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_subscriptions_next_billing",
            table: "digital_service_subscriptions",
            column: "next_billing_date");

        migrationBuilder.CreateIndex(
            name: "idx_digital_service_subscriptions_tenant_service_status",
            table: "digital_service_subscriptions",
            columns: new[] { "tenant_id", "service_id", "status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "digital_service_subscriptions");
    }
}
