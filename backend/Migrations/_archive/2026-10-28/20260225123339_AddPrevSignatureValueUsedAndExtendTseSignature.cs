using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddPrevSignatureValueUsedAndExtendTseSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: handle both "TseSignature" (PascalCase) and "tse_signature" (snake_case) column names
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'TseSignature') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""TseSignature"" TYPE character varying(2000);
                    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'tse_signature') THEN
                        ALTER TABLE payment_details ALTER COLUMN tse_signature TYPE character varying(2000);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'PrevSignatureValueUsed') THEN
                        ALTER TABLE payment_details ADD COLUMN ""PrevSignatureValueUsed"" character varying(2000) NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'PrevSignatureValueUsed') THEN
                        ALTER TABLE payment_details DROP COLUMN ""PrevSignatureValueUsed"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'TseSignature') THEN
                        ALTER TABLE payment_details ALTER COLUMN ""TseSignature"" TYPE character varying(100);
                    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'payment_details' AND column_name = 'tse_signature') THEN
                        ALTER TABLE payment_details ALTER COLUMN tse_signature TYPE character varying(100);
                    END IF;
                END $$;
            ");
        }
    }
}
