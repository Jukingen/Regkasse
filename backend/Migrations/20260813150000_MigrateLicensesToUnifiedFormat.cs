using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Creates <c>license_key_mappings</c> and rewrites legacy display keys
/// (<c>REGK-XXXXX-XXXXX-XXXXX</c>) to the unified format
/// <c>REGK-yyyyMMdd-{slug}-{8}</c> (system slug for issued licenses, tenant slug for sales).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260813150000_MigrateLicensesToUnifiedFormat")]
public partial class MigrateLicensesToUnifiedFormat : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "license_key_mappings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                old_license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                new_license_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                license_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                source_table = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_license_key_mappings", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_license_key_mappings_old_license_key",
            table: "license_key_mappings",
            column: "old_license_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_license_key_mappings_new_license_key",
            table: "license_key_mappings",
            column: "new_license_key");

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION pg_temp.regk_random8() RETURNS text
            LANGUAGE plpgsql AS $fn$
            DECLARE
                alphabet constant text := 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
                result text := '';
                i int;
            BEGIN
                FOR i IN 1..8 LOOP
                    result := result || substr(alphabet, 1 + (floor(random() * 36))::int, 1);
                END LOOP;
                RETURN result;
            END;
            $fn$;

            CREATE OR REPLACE FUNCTION pg_temp.regk_key_taken(candidate text) RETURNS boolean
            LANGUAGE sql AS $fn$
                SELECT EXISTS (
                    SELECT 1 FROM issued_licenses WHERE license_key = candidate
                    UNION ALL
                    SELECT 1 FROM license_sales WHERE license_key = candidate
                    UNION ALL
                    SELECT 1 FROM activated_licenses WHERE license_key = candidate
                    UNION ALL
                    SELECT 1 FROM tenants WHERE license_key = candidate
                    UNION ALL
                    SELECT 1 FROM license_key_mappings
                    WHERE old_license_key = candidate OR new_license_key = candidate
                );
            $fn$;

            CREATE OR REPLACE FUNCTION pg_temp.regk_alloc_unified(date_part text, slug text) RETURNS text
            LANGUAGE plpgsql AS $fn$
            DECLARE
                candidate text;
                attempts int := 0;
            BEGIN
                LOOP
                    attempts := attempts + 1;
                    IF attempts > 64 THEN
                        RAISE EXCEPTION 'Could not allocate a unique unified license key for slug %', slug;
                    END IF;
                    candidate := 'REGK-' || date_part || '-' || slug || '-' || pg_temp.regk_random8();
                    EXIT WHEN NOT pg_temp.regk_key_taken(candidate);
                END LOOP;
                RETURN candidate;
            END;
            $fn$;

            -- issued_licenses: legacy display → REGK-{expiry}-system-{8}
            DO $issued$
            DECLARE
                rec record;
                new_key text;
                date_part text;
                kind text;
            BEGIN
                FOR rec IN
                    SELECT il.id, il.license_key, il.expiry_at_utc
                    FROM issued_licenses il
                    WHERE il.license_key ~* '^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$'
                      AND il.license_key !~ '^REGK-[0-9]{8}-'
                LOOP
                    date_part := to_char((rec.expiry_at_utc AT TIME ZONE 'UTC'), 'YYYYMMDD');
                    new_key := pg_temp.regk_alloc_unified(date_part, 'system');
                    kind := CASE
                        WHEN EXISTS (SELECT 1 FROM tenants t WHERE t.license_key = rec.license_key)
                             OR EXISTS (SELECT 1 FROM license_sales s WHERE s.license_key = rec.license_key)
                            THEN 'both'
                        ELSE 'system'
                    END;

                    INSERT INTO license_key_mappings (
                        id, old_license_key, new_license_key, license_kind, source_table, source_id, created_at_utc)
                    VALUES (
                        gen_random_uuid(), rec.license_key, new_key, kind, 'issued_licenses', rec.id, now());

                    UPDATE issued_licenses
                    SET license_key = new_key
                    WHERE id = rec.id;

                    UPDATE activated_licenses
                    SET license_key = new_key
                    WHERE license_key = rec.license_key;

                    UPDATE tenants
                    SET license_key = new_key, updated_at = now()
                    WHERE license_key = rec.license_key;
                END LOOP;
            END;
            $issued$;

            -- license_sales: non-unified keys → REGK-{valid_until}-{tenant.slug}-{8}
            -- tenants.Slug was created as quoted PascalCase ("Slug"); some DBs may already use slug.
            DO $sales$
            DECLARE
                rec record;
                new_key text;
                date_part text;
                slug text;
                existing_new text;
            BEGIN
                FOR rec IN
                    SELECT s.id, s.license_key, s.valid_until_utc,
                           COALESCE(to_jsonb(t)->>'slug', to_jsonb(t)->>'Slug') AS tenant_slug
                    FROM license_sales s
                    INNER JOIN tenants t ON t.id = s.tenant_id
                    WHERE s.license_key !~ '^REGK-[0-9]{8}-[a-z0-9]+(-[a-z0-9]+)*-[A-Z0-9]{8}$'
                      AND coalesce(COALESCE(to_jsonb(t)->>'slug', to_jsonb(t)->>'Slug'), '') <> ''
                LOOP
                    SELECT m.new_license_key INTO existing_new
                    FROM license_key_mappings m
                    WHERE m.old_license_key = rec.license_key
                    LIMIT 1;

                    IF existing_new IS NOT NULL THEN
                        new_key := existing_new;
                        UPDATE license_key_mappings
                        SET license_kind = 'both'
                        WHERE old_license_key = rec.license_key
                          AND license_kind <> 'both';
                    ELSE
                        slug := lower(rec.tenant_slug);
                        date_part := to_char((rec.valid_until_utc AT TIME ZONE 'UTC'), 'YYYYMMDD');
                        new_key := pg_temp.regk_alloc_unified(date_part, slug);

                        INSERT INTO license_key_mappings (
                            id, old_license_key, new_license_key, license_kind, source_table, source_id, created_at_utc)
                        VALUES (
                            gen_random_uuid(), rec.license_key, new_key, 'tenant', 'license_sales', rec.id, now());
                    END IF;

                    UPDATE license_sales
                    SET license_key = new_key, updated_at = now()
                    WHERE id = rec.id;

                    UPDATE tenants
                    SET license_key = new_key, updated_at = now()
                    WHERE license_key = rec.license_key;
                END LOOP;
            END;
            $sales$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Restore previous keys from the mapping table (best-effort).
            UPDATE issued_licenses il
            SET license_key = m.old_license_key
            FROM license_key_mappings m
            WHERE m.source_table = 'issued_licenses'
              AND m.source_id = il.id
              AND il.license_key = m.new_license_key;

            UPDATE license_sales s
            SET license_key = m.old_license_key, updated_at = now()
            FROM license_key_mappings m
            WHERE m.source_table = 'license_sales'
              AND m.source_id = s.id
              AND s.license_key = m.new_license_key;

            UPDATE activated_licenses a
            SET license_key = m.old_license_key
            FROM license_key_mappings m
            WHERE a.license_key = m.new_license_key
              AND m.source_table = 'issued_licenses';

            UPDATE tenants t
            SET license_key = m.old_license_key, updated_at = now()
            FROM license_key_mappings m
            WHERE t.license_key = m.new_license_key;
            """);

        migrationBuilder.DropTable(name: "license_key_mappings");
    }
}
