using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Product tablosunu RKSV uyumlu hale getirmek için migration
    /// Sadece gerekli minimum değişiklikler
    /// </summary>
    public partial class UpdateProductTableForRKSV : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut alanları güncelle (sadece gerekli olanlar)
            migrationBuilder.AlterColumn<string>(
                name: "tax_type",
                table: "products",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "products",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "products",
                type: "text",
                nullable: true);

            // Yeni index'ler ekle (sadece gerekli olanlar)
            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true,
                filter: "\"barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_products_tax_type",
                table: "products",
                column: "tax_type");

            // Check constraint'ler ekle (sadece gerekli olanlar)
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

            // Yorum ekle
            migrationBuilder.Sql("COMMENT ON TABLE products IS 'RKSV uyumlu ürün tablosu - Avusturya kasa sistemi standartlarına uygun'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.tax_type IS 'RKSV vergi tipi: Standard(20%), Reduced(10%), Special(13%)'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.category IS 'Ürün kategorisi'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.barcode IS 'Barkod (EAN-13, UPC, vb.)'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.name IS 'Ürün adı'");
            migrationBuilder.Sql("COMMENT ON COLUMN products.description IS 'Ürün açıklaması'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Check constraint'leri kaldır
            migrationBuilder.DropCheckConstraint(
                name: "CK_products_price_positive",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_stock_quantity_non_negative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_min_stock_level_non_negative",
                table: "products");

            // Index'leri kaldır
            migrationBuilder.DropIndex(
                name: "IX_products_barcode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tax_type",
                table: "products");

            // Alanları eski haline getir
            migrationBuilder.AlterColumn<string>(
                name: "tax_type",
                table: "products",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "products",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "barcode",
                table: "products",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "products",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "products",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
