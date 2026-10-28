using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductTableNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Alan isimlerini düzelt (PostgreSQL naming convention)
            migrationBuilder.RenameColumn(
                name: "Cost",
                table: "products",
                newName: "cost");

            migrationBuilder.RenameColumn(
                name: "TaxRate",
                table: "products",
                newName: "tax_rate");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "products",
                newName: "category_id");

            // 2. Alan uzunluklarını güncelle (RKSV uyumluluğu için)
            migrationBuilder.AlterColumn<string>(
                name: "tax_type",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            // 3. Yeni index'leri ekle
            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                filter: "\"barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_products_tax_type",
                table: "products",
                column: "tax_type");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            // 4. Unique barcode index'i
            migrationBuilder.CreateIndex(
                name: "UQ_products_barcode",
                table: "products",
                column: "barcode",
                unique: true,
                filter: "\"barcode\" IS NOT NULL");

            // 5. Check constraint'leri ekle
            migrationBuilder.AddCheckConstraint(
                name: "CK_products_price_positive",
                table: "products",
                sql: "\"price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_stock_quantity_non_negative",
                table: "products",
                sql: "\"stock_quantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_min_stock_level_non_negative",
                table: "products",
                sql: "\"min_stock_level\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_cost_non_negative",
                table: "products",
                sql: "\"cost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_tax_rate_range",
                table: "products",
                sql: "\"tax_rate\" >= 0 AND \"tax_rate\" <= 100");

            // 6. Yorumları ekle
            migrationBuilder.Sql("COMMENT ON TABLE products IS 'RKSV uyumlu ürün tablosu - PostgreSQL naming convention standartlarına uygun'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.tax_type IS 'RKSV vergi tipi: Standard(20%), Reduced(10%), Special(13%)'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.category IS 'Ürün kategorisi'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.category_id IS 'Kategori ID referansı'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.barcode IS 'Barkod (EAN-13, UPC, vb.)'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.name IS 'Ürün adı'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.description IS 'Ürün açıklaması'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.cost IS 'Ürün maliyeti'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.tax_rate IS 'Vergi oranı (%)'");

            // 7. Default değerler ekle
            migrationBuilder.AlterColumn<decimal>(
                name: "cost",
                table: "products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0.00m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "tax_rate",
                table: "products",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 20.00m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<int>(
                name: "min_stock_level",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Check constraint'leri kaldır
            migrationBuilder.DropCheckConstraint(
                name: "CK_products_price_positive",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_stock_quantity_non_negative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_min_stock_level_non_negative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_cost_non_negative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_tax_rate_range",
                table: "products");

            // 2. Index'leri kaldır
            migrationBuilder.DropIndex(
                name: "IX_products_barcode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tax_type",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "UQ_products_barcode",
                table: "products");

            // 3. Alan isimlerini eski haline getir
            migrationBuilder.RenameColumn(
                name: "cost",
                table: "products",
                newName: "Cost");

            migrationBuilder.RenameColumn(
                name: "tax_rate",
                table: "products",
                newName: "TaxRate");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "products",
                newName: "CategoryId");

            // 4. Alanları eski haline getir
            migrationBuilder.AlterColumn<string>(
                name: "tax_type",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // 5. Default değerleri kaldır
            migrationBuilder.AlterColumn<decimal>(
                name: "Cost",
                table: "products",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0.00m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "products",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldDefaultValue: 20.00m);

            migrationBuilder.AlterColumn<int>(
                name: "min_stock_level",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
