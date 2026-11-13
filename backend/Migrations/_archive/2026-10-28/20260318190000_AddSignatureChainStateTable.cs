using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Adds signature_chain_state table for concurrency-safe TSE signature chaining per register.
    /// One row per RegisterId (KassenId); LastSignature and LastCounter updated under FOR UPDATE lock.
    /// </summary>
    public partial class AddSignatureChainStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Production-readiness fix: table may already exist (created by earlier migration ordering drift).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'signature_chain_state') THEN
                        CREATE TABLE signature_chain_state (
                            id uuid NOT NULL,
                            register_id character varying(50) NOT NULL,
                            last_signature character varying(4000),
                            last_counter integer NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            CONSTRAINT PK_signature_chain_state PRIMARY KEY (id)
                        );

                        CREATE UNIQUE INDEX ""IX_signature_chain_state_register_id""
                            ON signature_chain_state (register_id);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "signature_chain_state");
        }
    }
}
