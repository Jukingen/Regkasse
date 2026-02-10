using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System;
using System.Linq;

namespace KasseAPI_Final
{
    public class SeedDemoData
    {
        public static async Task SeedAsync()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=kassedb;Username=postgres;Password=Juke");
            
            using var context = new AppDbContext(optionsBuilder.Options);
            
            Console.WriteLine("Demo veriler ekleniyor...");
             
            // Kategoriler eklea
            await SeedCategoriesAsync(context);
            
            // Ürünler ekle
            await SeedProductsAsync(context);
            
            Console.WriteLine("Demo veriler başarıyla eklendi!");
        }
        
        private static async Task SeedCategoriesAsync(AppDbContext context)
        {
            if (await context.Categories.AnyAsync())
            {
                Console.WriteLine("Kategoriler zaten mevcut, atlanıyor...");
                return;
            }
            
            var categories = new[]
            {
                new Category { Id = Guid.NewGuid(), Name = "Getränke", Description = "Alkoholfreie und alkoholische Getränke", Color = "#3498db", Icon = "wine", SortOrder = 1, IsActive = true },
                new Category { Id = Guid.NewGuid(), Name = "Speisen", Description = "Hauptgerichte und Vorspeisen", Color = "#e74c3c", Icon = "restaurant", SortOrder = 2, IsActive = true },
                new Category { Id = Guid.NewGuid(), Name = "Desserts", Description = "Süße Nachspeisen und Kuchen", Color = "#f39c12", Icon = "ice-cream", SortOrder = 3, IsActive = true },
                new Category { Id = Guid.NewGuid(), Name = "Snacks", Description = "Kleine Zwischenmahlzeiten", Color = "#27ae60", Icon = "fast-food", SortOrder = 4, IsActive = true },
                new Category { Id = Guid.NewGuid(), Name = "Kaffee & Tee", Description = "Heiße Getränke", Color = "#8e44ad", Icon = "cafe", SortOrder = 5, IsActive = true }
            };
            
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
            
            Console.WriteLine($"{categories.Length} kategori eklendi.");
        }
        
        private static async Task SeedProductsAsync(AppDbContext context)
        {
            if (await context.Products.AnyAsync())
            {
                Console.WriteLine("Ürünler zaten mevcut, atlanıyor...");
                return;
            }
            
            var categories = await context.Categories.ToListAsync();
            var getränkeCategory = categories.FirstOrDefault(c => c.Name == "Getränke");
            var speisenCategory = categories.FirstOrDefault(c => c.Name == "Speisen");
            var dessertsCategory = categories.FirstOrDefault(c => c.Name == "Desserts");
            var snacksCategory = categories.FirstOrDefault(c => c.Name == "Snacks");
            var kaffeeCategory = categories.FirstOrDefault(c => c.Name == "Kaffee & Tee");
            
            var products = new List<Product>();
            
            // Getränke - Standard Tax (20%)
            if (getränkeCategory != null)
            {
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Cola 0.33L", Description = "Erfrischendes Cola-Getränk", Price = 2.50m, TaxType = "Standard", Category = "Getränke", IsActive = true, StockQuantity = 100 },
                    new Product { Id = Guid.NewGuid(), Name = "Bier 0.5L", Description = "Helles Lagerbier", Price = 4.80m, TaxType = "Standard", Category = "Getränke", IsActive = true, StockQuantity = 50 },
                    new Product { Id = Guid.NewGuid(), Name = "Wein 0.2L", Description = "Rotwein aus Österreich", Price = 6.50m, TaxType = "Standard", Category = "Getränke", IsActive = true, StockQuantity = 30 },
                    new Product { Id = Guid.NewGuid(), Name = "Mineralwasser 0.5L", Description = "Natürliches Mineralwasser", Price = 1.80m, TaxType = "Standard", Category = "Getränke", IsActive = true, StockQuantity = 80 }
                });
            }
            
            // Speisen - Standard Tax (20%)
            if (speisenCategory != null)
            {
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Wiener Schnitzel", Description = "Klassisches Wiener Schnitzel mit Pommes", Price = 18.90m, TaxType = "Standard", Category = "Speisen", IsActive = true, StockQuantity = 25 },
                    new Product { Id = Guid.NewGuid(), Name = "Gulasch", Description = "Traditionelles Rindergulasch mit Semmelknödel", Price = 16.50m, TaxType = "Standard", Category = "Speisen", IsActive = true, StockQuantity = 20 },
                    new Product { Id = Guid.NewGuid(), Name = "Salat", Description = "Gemischter Salat mit hausgemachtem Dressing", Price = 8.90m, TaxType = "Standard", Category = "Speisen", IsActive = true, StockQuantity = 15 }
                });
            }
            
            // Desserts - Reduced Tax (10%)
            if (dessertsCategory != null)
            {
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Apfelstrudel", Description = "Hausgemachter Apfelstrudel mit Vanillesauce", Price = 6.90m, TaxType = "Reduced", Category = "Desserts", IsActive = true, StockQuantity = 12 },
                    new Product { Id = Guid.NewGuid(), Name = "Sachertorte", Description = "Original Wiener Sachertorte", Price = 7.50m, TaxType = "Reduced", Category = "Desserts", IsActive = true, StockQuantity = 10 },
                    new Product { Id = Guid.NewGuid(), Name = "Eisbecher", Description = "3 Kugeln Eis mit Sahne und Schokoladensauce", Price = 5.90m, TaxType = "Reduced", Category = "Desserts", IsActive = true, StockQuantity = 20 }
                });
            }
            
            // Snacks - Reduced Tax (10%)
            if (snacksCategory != null)
            {
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Chips", Description = "Kartoffelchips mit Salz", Price = 3.50m, TaxType = "Reduced", Category = "Snacks", IsActive = true, StockQuantity = 40 },
                    new Product { Id = Guid.NewGuid(), Name = "Nüsse", Description = "Gemischte Nüsse 100g", Price = 4.20m, TaxType = "Reduced", Category = "Snacks", IsActive = true, StockQuantity = 25 },
                    new Product { Id = Guid.NewGuid(), Name = "Schokolade", Description = "Milchschokolade 100g", Price = 2.80m, TaxType = "Reduced", Category = "Snacks", IsActive = true, StockQuantity = 35 }
                });
            }
            
            // Kaffee & Tee - Special Tax (13%)
            if (kaffeeCategory != null)
            {
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Espresso", Description = "Starker italienischer Espresso", Price = 3.20m, TaxType = "Special", Category = "Kaffee & Tee", IsActive = true, StockQuantity = 60 },
                    new Product { Id = Guid.NewGuid(), Name = "Cappuccino", Description = "Cappuccino mit Milchschaum", Price = 4.50m, TaxType = "Special", Category = "Kaffee & Tee", IsActive = true, StockQuantity = 45 },
                    new Product { Id = Guid.NewGuid(), Name = "Tee", Description = "Kräutertee aus Österreich", Price = 3.80m, TaxType = "Special", Category = "Kaffee & Tee", IsActive = true, StockQuantity = 30 }
                });
            }
            
            context.Products.AddRange(products);
            await context.SaveChangesAsync();
            
            Console.WriteLine($"{products.Count} ürün eklendi.");
            Console.WriteLine("Vergi grupları:");
            Console.WriteLine($"- Standard (20%): {products.Count(p => p.TaxType == "Standard")} ürün");
            Console.WriteLine($"- Reduced (10%): {products.Count(p => p.TaxType == "Reduced")} ürün");
            Console.WriteLine($"- Special (13%): {products.Count(p => p.TaxType == "Special")} ürün");
        }
    }
}
