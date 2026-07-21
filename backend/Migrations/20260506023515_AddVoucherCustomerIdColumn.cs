using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherCustomerIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_customer_id",
                table: "vouchers",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_vouchers_customers_customer_id",
                table: "vouchers",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vouchers_customers_customer_id",
                table: "vouchers");

            migrationBuilder.DropIndex(
                name: "IX_vouchers_customer_id",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "vouchers");
        }
    }
}
