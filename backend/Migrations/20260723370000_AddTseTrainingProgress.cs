using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723370000_AddTseTrainingProgress")]
public partial class AddTseTrainingProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_training_progress",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                module_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_started = table.Column<bool>(type: "boolean", nullable: false),
                is_completed = table.Column<bool>(type: "boolean", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_training_progress", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_training_progress_user_module",
            table: "tse_training_progress",
            columns: new[] { "user_id", "module_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_training_progress");
    }
}
