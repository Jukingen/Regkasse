using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Repairs <c>elmah_error</c> so ElmahCore.Postgresql can insert rows.
    /// PgsqlErrorLog quotes the identifier as <c>"User"</c> (PascalCase); PostgreSQL treats
    /// that as a different column from <c>"user"</c>. A leftover lowercase <c>"user"</c>
    /// that is NOT NULL blocks Elmah inserts that only populate <c>"User"</c>.
    /// Idempotent: safe to re-apply.
    /// </summary>
    public partial class AddElmahUserColumn : Migration
    {
        /// <summary>
        /// Full ensure script: create <c>elmah_error</c> when missing, then ensure a single
        /// nullable <c>"User"</c> column (rename or add, then drop leftover <c>"user"</c>).
        /// </summary>
        public const string EnsureSql =
            """
            CREATE SEQUENCE IF NOT EXISTS elmah_error_sequence;

            CREATE TABLE IF NOT EXISTS elmah_error
            (
                errorid UUID NOT NULL,
                application VARCHAR(60) NOT NULL,
                host VARCHAR(50) NOT NULL,
                type VARCHAR(100) NOT NULL,
                source VARCHAR(60) NOT NULL,
                message VARCHAR(500) NOT NULL,
                "User" text NULL,
                statuscode INT NOT NULL,
                timeutc TIMESTAMP NOT NULL,
                sequence INT NOT NULL DEFAULT NEXTVAL('elmah_error_sequence'),
                allxml TEXT NOT NULL,
                CONSTRAINT pk_elmah_error PRIMARY KEY (errorid)
            );

            CREATE INDEX IF NOT EXISTS ix_elmah_error_app_time_seq ON elmah_error USING BTREE
            (
                application ASC,
                timeutc DESC,
                sequence DESC
            );

            DO $elmah_user$
            DECLARE
                has_pascal boolean;
                has_lower boolean;
            BEGIN
                IF to_regclass('public.elmah_error') IS NULL THEN
                    RETURN;
                END IF;

                SELECT EXISTS (
                    SELECT 1
                    FROM pg_attribute a
                    JOIN pg_class c ON c.oid = a.attrelid
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public'
                      AND c.relname = 'elmah_error'
                      AND a.attname = 'User'
                      AND a.attnum > 0
                      AND NOT a.attisdropped
                ) INTO has_pascal;

                SELECT EXISTS (
                    SELECT 1
                    FROM pg_attribute a
                    JOIN pg_class c ON c.oid = a.attrelid
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public'
                      AND c.relname = 'elmah_error'
                      AND a.attname = 'user'
                      AND a.attnum > 0
                      AND NOT a.attisdropped
                ) INTO has_lower;

                IF has_lower AND NOT has_pascal THEN
                    ALTER TABLE elmah_error RENAME COLUMN "user" TO "User";
                    has_pascal := true;
                    has_lower := false;
                ELSIF NOT has_pascal THEN
                    ALTER TABLE elmah_error ADD COLUMN "User" text NULL;
                    has_pascal := true;
                END IF;

                IF has_lower AND has_pascal THEN
                    UPDATE elmah_error SET "User" = "user" WHERE "User" IS NULL;
                    ALTER TABLE elmah_error DROP COLUMN "user";
                END IF;

                ALTER TABLE elmah_error ALTER COLUMN "User" TYPE text;
                ALTER TABLE elmah_error ALTER COLUMN "User" DROP NOT NULL;
            END
            $elmah_user$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(EnsureSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: dropping "User" would re-break ElmahCore inserts. The table may already
            // have logged rows and is not an EF-mapped entity.
        }
    }
}
