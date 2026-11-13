using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantFiskalyCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_fiskaly_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_protected = table.Column<string>(type: "text", nullable: false),
                    api_secret_protected = table.Column<string>(type: "text", nullable: false),
                    api_key_hint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    scu_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    environment = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "TEST"),
                    signing_certificate_der_base64 = table.Column<string>(type: "text", nullable: true),
                    issuer_certificates_der_base64 = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_fiskaly_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_fiskaly_credentials_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_fiskaly_credentials_tenant_id",
                table: "tenant_fiskaly_credentials",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_fiskaly_credentials");
        }
    }
}
