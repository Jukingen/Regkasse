using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717210000_AddTenantCustomizations")]
public partial class AddTenantCustomizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_customizations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                surface = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                primary_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                secondary_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                background_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                text_color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                font_family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                favicon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                pages_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[\"home\",\"menu\",\"about\",\"contact\"]"),
                features_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[\"live-menu\"]"),
                custom_css = table.Column<string>(type: "text", nullable: true),
                custom_js = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_customizations", x => x.id);
                table.ForeignKey(
                    name: "FK_tenant_customizations_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tenant_customizations_tenant_id",
            table: "tenant_customizations",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_tenant_customizations_tenant_surface",
            table: "tenant_customizations",
            columns: new[] { "tenant_id", "surface" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tenant_customizations");
    }
}
