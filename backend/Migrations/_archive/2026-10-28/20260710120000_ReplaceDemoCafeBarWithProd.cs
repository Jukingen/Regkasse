using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDemoCafeBarWithProd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate legacy demo tenants (cafe/bar) to dev/prod presets.
            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET "Name" = 'Production',
                    "Slug" = 'prod',
                    email = 'admin@prod.regkasse.at',
                    updated_at = NOW()
                WHERE "Slug" = 'cafe';

                UPDATE "AspNetUsers"
                SET "UserName" = 'admin@prod.regkasse.at',
                    "NormalizedUserName" = 'ADMIN@PROD.REGKASSE.AT',
                    "Email" = 'admin@prod.regkasse.at',
                    "NormalizedEmail" = 'ADMIN@PROD.REGKASSE.AT',
                    "UpdatedAt" = NOW()
                WHERE lower("Email") = 'admin@cafe.regkasse.at';

                UPDATE tenants
                SET is_active = false,
                    status = 'archived',
                    updated_at = NOW()
                WHERE "Slug" = 'bar';

                UPDATE "AspNetUsers"
                SET is_active = false,
                    "UpdatedAt" = NOW()
                WHERE lower("Email") = 'admin@bar.regkasse.at';

                INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
                SELECT 'b0000001-0001-4001-8001-000000000002'::uuid,
                       'Production',
                       'prod',
                       NOW(),
                       true,
                       'active',
                       'admin@prod.regkasse.at'
                WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'prod');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migration is not reversed (legacy cafe/bar may have been renamed).
        }
    }
}
