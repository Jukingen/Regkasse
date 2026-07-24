using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using KasseAPI_Final.Data;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723410000_AddTseKnowledgeBase")]
public partial class AddTseKnowledgeBase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tse_knowledge_articles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_faq = table.Column<bool>(type: "boolean", nullable: false),
                view_count = table.Column<int>(type: "integer", nullable: false),
                rating_sum = table.Column<int>(type: "integer", nullable: false),
                rating_count = table.Column<int>(type: "integer", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_published = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_knowledge_articles", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_knowledge_articles_slug",
            table: "tse_knowledge_articles",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_tse_knowledge_articles_published_faq_views",
            table: "tse_knowledge_articles",
            columns: new[] { "is_published", "is_faq", "view_count" });

        migrationBuilder.CreateTable(
            name: "tse_knowledge_feedback",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                rating = table.Column<int>(type: "integer", nullable: false),
                actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tse_knowledge_feedback", x => x.id);
                table.ForeignKey(
                    name: "FK_tse_knowledge_feedback_articles",
                    column: x => x.article_id,
                    principalTable: "tse_knowledge_articles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_tse_knowledge_feedback_article",
            table: "tse_knowledge_feedback",
            column: "article_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tse_knowledge_feedback");
        migrationBuilder.DropTable(name: "tse_knowledge_articles");
    }
}
