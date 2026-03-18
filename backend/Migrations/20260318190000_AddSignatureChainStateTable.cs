using System;
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
            migrationBuilder.CreateTable(
                name: "signature_chain_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_signature = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    last_counter = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_chain_state", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_signature_chain_state_register_id",
                table: "signature_chain_state",
                column: "register_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "signature_chain_state");
        }
    }
}
