using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogImpersonationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "impersonated_by",
                table: "audit_logs",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "impersonated_tenant",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_impersonated_by",
                table: "audit_logs",
                column: "impersonated_by");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_impersonated_tenant",
                table: "audit_logs",
                column: "impersonated_tenant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_impersonated_by",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_impersonated_tenant",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "impersonated_by",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "impersonated_tenant",
                table: "audit_logs");
        }
    }
}
