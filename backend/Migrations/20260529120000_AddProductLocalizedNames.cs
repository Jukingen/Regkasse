using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Adds de/en/tr localized product name and description columns; backfills from legacy name/description.</summary>
public partial class AddProductLocalizedNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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

        migrationBuilder.Sql("""
            UPDATE products
            SET name_de = name,
                description_de = description
            WHERE name_de IS NULL OR name_de = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "name_de", table: "products");
        migrationBuilder.DropColumn(name: "name_en", table: "products");
        migrationBuilder.DropColumn(name: "name_tr", table: "products");
        migrationBuilder.DropColumn(name: "description_de", table: "products");
        migrationBuilder.DropColumn(name: "description_en", table: "products");
        migrationBuilder.DropColumn(name: "description_tr", table: "products");
    }
}
