using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Registrierkasse_API.Migrations
{
    /// <inheritdoc />
    public partial class AutoSyncMigration_2024 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerDetails_CustomerDetailsId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PaymentDetails_PaymentDetailsId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_Orders_current_order_id",
                table: "tables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentDetails",
                table: "PaymentDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerDetails",
                table: "CustomerDetails");

            migrationBuilder.RenameTable(
                name: "PaymentDetails",
                newName: "PaymentDetailsSet");

            migrationBuilder.RenameTable(
                name: "CustomerDetails",
                newName: "CustomerDetailsSet");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_paid",
                table: "tables",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "empty",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<decimal>(
                name: "current_total",
                table: "tables",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "current_cart_id",
                table: "tables",
                type: "character varying(50)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_split_count",
                table: "tables",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "tables",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "service_charge_percentage",
                table: "tables",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "split_bill_enabled",
                table: "tables",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tip_percentage",
                table: "tables",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargeAmount",
                table: "Carts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TableId",
                table: "Carts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TipAmount",
                table: "Carts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "payment_methods",
                table: "Carts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "split_amount",
                table: "Carts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "split_count",
                table: "Carts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "account_type",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "login_count",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentDetailsSet",
                table: "PaymentDetailsSet",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerDetailsSet",
                table: "CustomerDetailsSet",
                column: "id");

            migrationBuilder.CreateTable(
                name: "DemoUserLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoUserLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedBy = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tables_current_cart_id",
                table: "tables",
                column: "current_cart_id");

            migrationBuilder.CreateIndex(
                name: "IX_tables_number",
                table: "tables",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_status",
                table: "tables",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_TableId",
                table: "Carts",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_tables_TableId",
                table: "Carts",
                column: "TableId",
                principalTable: "tables",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerDetailsSet_CustomerDetailsId",
                table: "Invoices",
                column: "CustomerDetailsId",
                principalTable: "CustomerDetailsSet",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PaymentDetailsSet_PaymentDetailsId",
                table: "Invoices",
                column: "PaymentDetailsId",
                principalTable: "PaymentDetailsSet",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_Carts_current_cart_id",
                table: "tables",
                column: "current_cart_id",
                principalTable: "Carts",
                principalColumn: "CartId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_Orders_current_order_id",
                table: "tables",
                column: "current_order_id",
                principalTable: "Orders",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carts_tables_TableId",
                table: "Carts");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerDetailsSet_CustomerDetailsId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PaymentDetailsSet_PaymentDetailsId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_Carts_current_cart_id",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_Orders_current_order_id",
                table: "tables");

            migrationBuilder.DropTable(
                name: "DemoUserLogs");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_tables_current_cart_id",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_number",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_status",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_Carts_TableId",
                table: "Carts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentDetailsSet",
                table: "PaymentDetailsSet");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerDetailsSet",
                table: "CustomerDetailsSet");

            migrationBuilder.DropColumn(
                name: "current_cart_id",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "max_split_count",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "service_charge_percentage",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "split_bill_enabled",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "tip_percentage",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "ServiceChargeAmount",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "TipAmount",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "payment_methods",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "split_amount",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "split_count",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "account_type",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "login_count",
                table: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "PaymentDetailsSet",
                newName: "PaymentDetails");

            migrationBuilder.RenameTable(
                name: "CustomerDetailsSet",
                newName: "CustomerDetails");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_paid",
                table: "tables",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "empty");

            migrationBuilder.AlterColumn<decimal>(
                name: "current_total",
                table: "tables",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentDetails",
                table: "PaymentDetails",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerDetails",
                table: "CustomerDetails",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerDetails_CustomerDetailsId",
                table: "Invoices",
                column: "CustomerDetailsId",
                principalTable: "CustomerDetails",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PaymentDetails_PaymentDetailsId",
                table: "Invoices",
                column: "PaymentDetailsId",
                principalTable: "PaymentDetails",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_Orders_current_order_id",
                table: "tables",
                column: "current_order_id",
                principalTable: "Orders",
                principalColumn: "id");
        }
    }
}
