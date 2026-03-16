using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseAuditEventFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "action_type",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_display_name",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "changes",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "action_type",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_display_name",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "changes",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "audit_logs");
        }
    }
}
