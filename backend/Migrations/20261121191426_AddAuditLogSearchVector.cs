using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_email",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "audit_logs",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('simple', replace(replace(replace(coalesce(action, '') || ' ' || coalesce(entity_type, '') || ' ' || coalesce(description, '') || ' ' || coalesce(user_email, ''), '_', ' '), '@', ' '), '.', ' '))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_search_vector",
                table: "audit_logs",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.Sql(
                """
                UPDATE audit_logs AS a
                SET user_email = u."Email"
                FROM "AspNetUsers" AS u
                WHERE a.user_id = u."Id"
                  AND a.user_email IS NULL
                  AND u."Email" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_search_vector",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "user_email",
                table: "audit_logs");
        }
    }
}
