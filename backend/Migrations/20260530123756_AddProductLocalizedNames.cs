using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLocalizedNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description_de",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_en",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_tr",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_de",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_tr",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE products
                SET name_de = name,
                    description_de = description
                WHERE name_de IS NULL OR name_de = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "approval_token_hash",
                table: "manual_restore_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description_de",
                table: "products");

            migrationBuilder.DropColumn(
                name: "description_en",
                table: "products");

            migrationBuilder.DropColumn(
                name: "description_tr",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name_de",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name_en",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name_tr",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "approval_token_hash",
                table: "manual_restore_requests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
