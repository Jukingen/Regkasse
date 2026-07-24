using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Tenant-scoped Austrian MwSt tax group catalog (flexible rates including 4.9% / 13%).</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260724120000_AddTaxGroups")]
public partial class AddTaxGroups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tax_groups",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                group_type = table.Column<int>(type: "integer", nullable: true),
                austrian_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tax_groups", x => x.id);
                table.CheckConstraint("CK_tax_groups_rate_range", "rate >= 0 AND rate <= 100");
                table.ForeignKey(
                    name: "FK_tax_groups_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tax_groups_tax_groups_replaced_by",
                    column: x => x.replaced_by,
                    principalTable: "tax_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tax_groups_tenant_id",
            table: "tax_groups",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_tax_groups_tenant_id_austrian_code",
            table: "tax_groups",
            columns: new[] { "tenant_id", "austrian_code" },
            unique: true,
            filter: "austrian_code IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_tax_groups_tenant_id_is_active_is_default",
            table: "tax_groups",
            columns: new[] { "tenant_id", "is_active", "is_default" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tax_groups");
    }
}
