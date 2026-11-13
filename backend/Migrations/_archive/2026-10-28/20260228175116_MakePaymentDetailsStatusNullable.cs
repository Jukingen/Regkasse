using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentDetailsStatusNullable : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// payment_details: Tüm model-dışı NOT NULL kolonları nullable yap (Status vb.).
        /// Dinamik: information_schema üzerinden döngü ile tüm NOT NULL kolonlara DROP NOT NULL uygulanır.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN
                        SELECT column_name
                        FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'payment_details' AND is_nullable = 'NO'
                    LOOP
                        BEGIN
                            EXECUTE format('ALTER TABLE payment_details ALTER COLUMN %I DROP NOT NULL', r.column_name);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Could not alter column %: %', r.column_name, SQLERRM;
                        END;
                    END LOOP;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
