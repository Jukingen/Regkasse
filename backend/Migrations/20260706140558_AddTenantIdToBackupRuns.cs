using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToBackupRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "backup_runs",
                type: "uuid",
                nullable: true);

            // Backfill tenant scope from legacy idempotency_key encoding (manual/import prefixes).
            migrationBuilder.Sql(
                """
                UPDATE backup_runs br
                SET tenant_id = parsed.tenant_id
                FROM (
                    SELECT id,
                        CASE
                            WHEN idempotency_key ~* '^manual-tenant-' THEN
                                substring(idempotency_key FROM '^manual-tenant-([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})-')::uuid
                            WHEN idempotency_key ~* '^import-tenant-' THEN
                                substring(idempotency_key FROM '^import-tenant-([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})-')::uuid
                            ELSE NULL
                        END AS tenant_id
                    FROM backup_runs
                    WHERE tenant_id IS NULL
                      AND idempotency_key IS NOT NULL
                ) parsed
                WHERE br.id = parsed.id
                  AND parsed.tenant_id IS NOT NULL
                  AND EXISTS (SELECT 1 FROM tenants t WHERE t.id = parsed.tenant_id);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_tenant_id",
                table: "backup_runs",
                column: "tenant_id",
                filter: "tenant_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_backup_runs_tenants_tenant_id",
                table: "backup_runs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backup_runs_tenants_tenant_id",
                table: "backup_runs");

            migrationBuilder.DropIndex(
                name: "ix_backup_runs_tenant_id",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "backup_runs");
        }
    }
}
