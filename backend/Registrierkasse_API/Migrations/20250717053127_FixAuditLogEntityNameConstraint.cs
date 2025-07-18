using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Registrierkasse_API.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditLogEntityNameConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterState",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "BeforeState",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "OperationLogs");

            migrationBuilder.RenameColumn(
                name: "Summary",
                table: "OperationLogs",
                newName: "UserAgent");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "OperationLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_UserId",
                table: "OperationLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationLogs_AspNetUsers_UserId",
                table: "OperationLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationLogs_AspNetUsers_UserId",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_UserId",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "OperationLogs");

            migrationBuilder.RenameColumn(
                name: "UserAgent",
                table: "OperationLogs",
                newName: "Summary");

            migrationBuilder.AddColumn<string>(
                name: "AfterState",
                table: "OperationLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BeforeState",
                table: "OperationLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "OperationLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "OperationLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserRole",
                table: "OperationLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "OperationLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
