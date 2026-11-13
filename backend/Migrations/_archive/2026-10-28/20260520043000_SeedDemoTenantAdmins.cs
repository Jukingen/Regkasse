using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoTenantAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent tenant rows for local presets (dev, cafe, bar).
            // Admin users + memberships: DemoTenantAdminSeed at startup (Identity password hash).
            // Manual SQL: scripts/seed-demo-tenant-admins.sql
            var seededAt = "2026-05-20T00:00:00Z";

            migrationBuilder.Sql(
                $"""
                INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
                SELECT 'b0000001-0001-4001-8001-000000000001'::uuid, 'Development', 'dev', '{seededAt}'::timestamptz, true, 'active', 'admin@dev.regkasse.at'
                WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'dev');

                INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
                SELECT 'b0000001-0001-4001-8001-000000000002'::uuid, 'Test Cafe', 'cafe', '{seededAt}'::timestamptz, true, 'active', 'admin@cafe.regkasse.at'
                WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'cafe');

                INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
                SELECT 'b0000001-0001-4001-8001-000000000003'::uuid, 'Test Bar', 'bar', '{seededAt}'::timestamptz, true, 'active', 'admin@bar.regkasse.at'
                WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'bar');
                """);

            // Backfill membership + owner when admin user already exists (e.g. after partial seed).
            migrationBuilder.Sql(
                """
                INSERT INTO user_tenant_memberships (id, user_id, tenant_id, is_active, is_owner, created_at_utc)
                SELECT gen_random_uuid(), u."Id", t.id, true, true, NOW()
                FROM (VALUES
                    ('admin@dev.regkasse.at', 'dev'),
                    ('admin@cafe.regkasse.at', 'cafe'),
                    ('admin@bar.regkasse.at', 'bar')
                ) AS pair(email, slug)
                JOIN "AspNetUsers" u ON lower(u."Email") = lower(pair.email)
                JOIN tenants t ON t."Slug" = pair.slug
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM user_tenant_memberships m
                    WHERE m.user_id = u."Id" AND m.tenant_id = t.id AND m.is_active = true
                );

                UPDATE user_tenant_memberships m
                SET is_owner = false, updated_at_utc = NOW()
                FROM tenants t
                WHERE m.tenant_id = t.id
                  AND t."Slug" IN ('dev', 'cafe', 'bar')
                  AND m.is_active = true
                  AND m.is_owner = true
                  AND m.user_id NOT IN (
                      SELECT u."Id"
                      FROM "AspNetUsers" u
                      WHERE lower(u."Email") IN (
                          'admin@dev.regkasse.at',
                          'admin@cafe.regkasse.at',
                          'admin@bar.regkasse.at'
                      )
                  );

                UPDATE user_tenant_memberships m
                SET is_owner = true, updated_at_utc = NOW()
                FROM tenants t
                JOIN "AspNetUsers" u ON lower(u."Email") = 'admin@' || t."Slug" || '.regkasse.at'
                WHERE m.user_id = u."Id"
                  AND m.tenant_id = t.id
                  AND m.is_active = true
                  AND t."Slug" IN ('dev', 'cafe', 'bar');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT u."Id", r."Id"
                FROM "AspNetUsers" u
                CROSS JOIN "AspNetRoles" r
                WHERE r."Name" = 'Manager'
                  AND lower(u."Email") IN (
                      'admin@dev.regkasse.at',
                      'admin@cafe.regkasse.at',
                      'admin@bar.regkasse.at'
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AspNetUserRoles" ur
                      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data seed is not reversed (demo tenants/admins may be in use).
        }
    }
}
