using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723240000_AddTseBackups")]
public partial class AddTseBackups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_backups",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                payload = table.Column<byte[]>(type: "bytea", nullable: false),
                encryption_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                device_count = table.Column<int>(type: "integer", nullable: false),
                chain_count = table.Column<int>(type: "integer", nullable: false),
                receipt_sequence_count = table.Column<int>(type: "integer", nullable: false),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                notes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_backups", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_backups_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_backups_tenant_id",
            table: "tse_backups",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "idx_tse_backups_created_at",
            table: "tse_backups",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_tse_backups_tenant_created",
            table: "tse_backups",
            columns: new[] { "tenant_id", "created_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_backups");
    }
}
