using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>FON Ausfall / Wiederinbetriebnahme episode table.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729220000_AddRksvAusfallEpisodes")]
public partial class AddRksvAusfallEpisodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rksv_ausfall_episodes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                episode_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                operation_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                begruendung = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                beginn_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ende_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                outbox_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                external_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                certificate_serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                kassen_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                related_ausfall_episode_id = table.Column<Guid>(type: "uuid", nullable: true),
                operator_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                approved_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                last_error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rksv_ausfall_episodes", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_rksv_ausfall_episodes_tenant_id_status",
            table: "rksv_ausfall_episodes",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_rksv_ausfall_episodes_device_id",
            table: "rksv_ausfall_episodes",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "IX_rksv_ausfall_episodes_outbox_message_id",
            table: "rksv_ausfall_episodes",
            column: "outbox_message_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rksv_ausfall_episodes");
    }
}
