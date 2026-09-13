using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260910010000_AddOrderPreorderColumns")]
    public partial class AddOrderPreorderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_preorder",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "preorder_status",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "preorder_ready_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "preorder_collected_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preorder_customer_notes",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_payment_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_source_payment_id",
                table: "orders",
                column: "source_payment_id",
                unique: true,
                filter: "\"source_payment_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_is_preorder_preorder_status",
                table: "orders",
                columns: new[] { "tenant_id", "is_preorder", "preorder_status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_tenant_id_receipt_number",
                table: "orders",
                columns: new[] { "tenant_id", "receipt_number" },
                filter: "\"is_preorder\" = TRUE AND \"receipt_number\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_source_payment_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_tenant_id_is_preorder_preorder_status",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_tenant_id_receipt_number",
                table: "orders");

            migrationBuilder.DropColumn(name: "is_preorder", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_status", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_ready_at", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_collected_at", table: "orders");
            migrationBuilder.DropColumn(name: "preorder_customer_notes", table: "orders");
            migrationBuilder.DropColumn(name: "source_payment_id", table: "orders");
            migrationBuilder.DropColumn(name: "receipt_number", table: "orders");
            migrationBuilder.DropColumn(name: "tenant_id", table: "orders");
        }
    }
}
