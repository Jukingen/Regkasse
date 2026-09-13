using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260910020000_AddPreorderBesorgerzettel")]
    public partial class AddPreorderBesorgerzettel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preorder_number",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "preorder_paid_amount",
                table: "orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "preorder_remaining_amount",
                table: "orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "preorder_pickup_deadline",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preorder_pickup_weeks",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "last_preorder_payment_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_preorder_number",
                table: "orders",
                columns: new[] { "tenant_id", "preorder_number" },
                unique: true,
                filter: "\"is_preorder\" = TRUE AND \"preorder_number\" IS NOT NULL");

            migrationBuilder.AddColumn<int>(
                name: "preorder_pickup_deadline_weeks",
                table: "company_settings",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "preorder_cancellation_policy_text",
                table: "company_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_tenant_id_preorder_number",
                table: "orders");

            migrationBuilder.DropColumn(name: "preorder_number", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_paid_amount", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_remaining_amount", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_pickup_deadline", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_pickup_weeks", table: "orders");
            migrationBuilder.DropColumn(name: "last_preorder_payment_id", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_pickup_deadline_weeks", table: "company_settings");
            migrationBuilder.DropColumn(name: "preorder_cancellation_policy_text", table: "company_settings");
        }
    }
}
