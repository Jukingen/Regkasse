using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class FixSchemaConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_register_transactions_cash_registers_CashRegisterId1",
                table: "cash_register_transactions");

            migrationBuilder.DropIndex(
                name: "IX_cash_register_transactions_CashRegisterId1",
                table: "cash_register_transactions");

            migrationBuilder.DropColumn(
                name: "CashRegisterId1",
                table: "cash_register_transactions");

            // PostgreSQL needs explicit casting from character varying to uuid
            migrationBuilder.Sql("ALTER TABLE receipts ALTER COLUMN cash_register_id TYPE uuid USING cash_register_id::uuid;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashRegisterId",
                table: "invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // PostgreSQL needs explicit casting from character varying to uuid
            migrationBuilder.Sql("ALTER TABLE cash_register_transactions ALTER COLUMN \"CashRegisterId\" TYPE uuid USING \"CashRegisterId\"::uuid;");

            migrationBuilder.AddColumn<Guid>(
                name: "cash_register_id",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_orders_OrderId",
                table: "orders",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_transactions_cash_registers_CashRegisterId",
                table: "cash_register_transactions",
                column: "CashRegisterId",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_OrderId",
                table: "order_items",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_register_transactions_cash_registers_CashRegisterId",
                table: "cash_register_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_OrderId",
                table: "order_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_orders_OrderId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_items_OrderId",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "IX_cash_register_transactions_CashRegisterId",
                table: "cash_register_transactions");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "AspNetUsers");

            migrationBuilder.Sql("ALTER TABLE receipts ALTER COLUMN cash_register_id TYPE character varying(50) USING cash_register_id::text;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashRegisterId",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql("ALTER TABLE cash_register_transactions ALTER COLUMN \"CashRegisterId\" TYPE character varying(50) USING \"CashRegisterId\"::text;");

            migrationBuilder.AddColumn<Guid>(
                name: "CashRegisterId1",
                table: "cash_register_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_cash_register_transactions_CashRegisterId1",
                table: "cash_register_transactions",
                column: "CashRegisterId1");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_register_transactions_cash_registers_CashRegisterId1",
                table: "cash_register_transactions",
                column: "CashRegisterId1",
                principalTable: "cash_registers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
