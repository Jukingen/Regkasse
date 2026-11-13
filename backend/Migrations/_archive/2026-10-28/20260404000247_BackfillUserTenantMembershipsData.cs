using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUserTenantMembershipsData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: matches scripts/backfill-user-tenant-memberships.sql (legacy default tenant).
            migrationBuilder.Sql(
                """
                INSERT INTO user_tenant_memberships (id, user_id, tenant_id, is_active, created_at_utc)
                SELECT gen_random_uuid(), u."Id", '9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c'::uuid, true, NOW()
                FROM "AspNetUsers" u
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM user_tenant_memberships m
                    WHERE m.user_id = u."Id" AND m.is_active = true
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill is not reversed: removing rows could delete legitimate memberships.
        }
    }
}
