using Microsoft.EntityFrameworkCore;
using Registrierkasse.Models;

namespace Registrierkasse.Data
{
    public static class SeedProducts
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>());

            // Eğer ürünler zaten varsa, ekleme yapma
            if (await context.Products.AnyAsync())
            {
                return;
            }

            var products = new List<Product>
            {
                new Product
                {
                    Name = "Wiener Schnitzel",
                    Description = "Geleneksel Viyana usulü dana eti",
                    Price = 18.90m,
                    TaxType = TaxType.Standard,
                    Category = "Hauptgerichte",
                    Barcode = "9001234567890",
                    Unit = "Stück",
                    StockQuantity = 100
                },
                new Product
                {
                    Name = "Apfelstrudel",
                    Description = "Elmalı geleneksel Avusturya tatlısı",
                    Price = 6.50m,
                    TaxType = TaxType.Reduced,
                    Category = "Desserts",
                    Barcode = "9001234567891",
                    Unit = "Stück",
                    StockQuantity = 50
                },
                new Product
                {
                    Name = "Kaiserschmarrn",
                    Description = "İmparator tatlısı, geleneksel Avusturya tatlısı",
                    Price = 8.90m,
                    TaxType = TaxType.Reduced,
                    Category = "Desserts",
                    Barcode = "9001234567892",
                    Unit = "Portion",
                    StockQuantity = 30
                },
                new Product
                {
                    Name = "Sachertorte",
                    Description = "Çikolatalı Viyana keki",
                    Price = 5.90m,
                    TaxType = TaxType.Reduced,
                    Category = "Desserts",
                    Barcode = "9001234567893",
                    Unit = "Stück",
                    StockQuantity = 20
                },
                new Product
                {
                    Name = "Gulaschsuppe",
                    Description = "Macar usulü et çorbası",
                    Price = 7.50m,
                    TaxType = TaxType.Standard,
                    Category = "Suppen",
                    Barcode = "9001234567894",
                    Unit = "Portion",
                    StockQuantity = 40
                },
                new Product
                {
                    Name = "Tiroler Gröstl",
                    Description = "Patates, et ve soğanlı geleneksel yemek",
                    Price = 15.90m,
                    TaxType = TaxType.Standard,
                    Category = "Hauptgerichte",
                    Barcode = "9001234567895",
                    Unit = "Portion",
                    StockQuantity = 35
                },
                new Product
                {
                    Name = "Käsespätzle",
                    Description = "Peynirli Avusturya makarnası",
                    Price = 12.90m,
                    TaxType = TaxType.Standard,
                    Category = "Hauptgerichte",
                    Barcode = "9001234567896",
                    Unit = "Portion",
                    StockQuantity = 45
                },
                new Product
                {
                    Name = "Almdudler",
                    Description = "Avusturya'nın milli içeceği",
                    Price = 3.50m,
                    TaxType = TaxType.Standard,
                    Category = "Getränke",
                    Barcode = "9001234567897",
                    Unit = "Flasche",
                    StockQuantity = 100
                },
                new Product
                {
                    Name = "Mozartkugel",
                    Description = "Mozart çikolatası",
                    Price = 2.90m,
                    TaxType = TaxType.Reduced,
                    Category = "Süßigkeiten",
                    Barcode = "9001234567898",
                    Unit = "Stück",
                    StockQuantity = 200
                },
                new Product
                {
                    Name = "Kürbiskernöl",
                    Description = "Steiermark balkabağı çekirdeği yağı",
                    Price = 12.90m,
                    TaxType = TaxType.Standard,
                    Category = "Spezialitäten",
                    Barcode = "9001234567899",
                    Unit = "Flasche",
                    StockQuantity = 30
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
} 