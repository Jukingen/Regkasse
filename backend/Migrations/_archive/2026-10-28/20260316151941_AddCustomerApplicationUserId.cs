using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerApplicationUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "application_user_id",
                table: "customers",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_application_user_id",
                table: "customers",
                column: "application_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_AspNetUsers_application_user_id",
                table: "customers",
                column: "application_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: link customers to users where customer_number matched employee_number (convention used before explicit FK).
            migrationBuilder.Sql(@"
UPDATE customers c
SET application_user_id = u.""Id""
FROM ""AspNetUsers"" u
WHERE u.employee_number IS NOT NULL AND TRIM(u.employee_number) != ''
  AND c.customer_number = u.employee_number
  AND c.application_user_id IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_AspNetUsers_application_user_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_application_user_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "application_user_id",
                table: "customers");
        }
    }
}
