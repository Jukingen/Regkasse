using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUsernameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_username_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    old_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    new_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_username_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_username_history_AspNetUsers_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_username_history_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_changed_at_utc",
                table: "user_username_history",
                column: "changed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_changed_by_user_id",
                table: "user_username_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_user_id",
                table: "user_username_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_username_history_user_id_changed_at_utc",
                table: "user_username_history",
                columns: new[] { "user_id", "changed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_username_history");
        }
    }
}
