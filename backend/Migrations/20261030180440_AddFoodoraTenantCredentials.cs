using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodoraTenantCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "foodora_transport_type",
                table: "online_order_integrations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credential_owner",
                table: "online_order_integration_providers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "deployment");

            migrationBuilder.CreateTable(
                name: "online_order_integration_tenant_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    client_secret = table.Column<string>(type: "text", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    webhook_token = table.Column<string>(type: "text", nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: true),
                    access_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_online_order_integration_tenant_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_online_order_integration_tenant_credentials_tenants_tenant_~",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_online_order_integration_tenant_credentials_tenant_provider",
                table: "online_order_integration_tenant_credentials",
                columns: new[] { "tenant_id", "provider" },
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE online_order_integration_providers
                SET credential_owner = 'tenant'
                WHERE provider = 'foodora';

                INSERT INTO online_order_integration_providers
                    (id, provider, enabled, client_id, client_secret, credential_owner, created_at, is_active)
                SELECT gen_random_uuid(), 'foodora', FALSE, '', '', 'tenant', NOW(), TRUE
                WHERE NOT EXISTS (
                    SELECT 1 FROM online_order_integration_providers WHERE provider = 'foodora'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "online_order_integration_tenant_credentials");

            migrationBuilder.DropColumn(
                name: "foodora_transport_type",
                table: "online_order_integrations");

            migrationBuilder.DropColumn(
                name: "credential_owner",
                table: "online_order_integration_providers");
        }
    }
}
