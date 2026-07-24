using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723360000_AddTseBlockchainAnchors")]
public partial class AddTseBlockchainAnchors : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_blockchain_ledger_state",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                current_block_number = table.Column<long>(type: "bigint", nullable: false),
                tip_block_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                network_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_connected = table.Column<bool>(type: "boolean", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                total_transactions = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_blockchain_ledger_state", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tse_blockchain_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: true),
                transaction_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                block_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                block_number = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                signature_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                signature_preview = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                is_verified = table.Column<bool>(type: "boolean", nullable: false),
                verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_simulated = table.Column<bool>(type: "boolean", nullable: false),
                network_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_blockchain_records", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_blockchain_records_tenants",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_blockchain_records_tenant_created",
            table: "tse_blockchain_records",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_tse_blockchain_records_tenant_sig",
            table: "tse_blockchain_records",
            columns: new[] { "tenant_id", "signature_hash" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_blockchain_records");
        migrationBuilder.DropTable(name: "tse_blockchain_ledger_state");
    }
}
