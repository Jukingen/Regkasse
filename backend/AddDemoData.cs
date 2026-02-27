using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final
{
    public static class AddDemoData
    {
        public static async Task AddDemoDataAsync()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=dfgdfg#");
            
            using var context = new AppDbContext(optionsBuilder.Options);
            
            Console.WriteLine("Demo veriler ekleniyor...");
            
            // Kategoriler ekle
            if (!await context.Categories.AnyAsync())
            {
                var categories = new[]
                {
                    new Category { Id = Guid.NewGuid(), Name = "Getränke", Description = "Alkoholfreie und alkoholische Getränke", Color = "#3498db", Icon = "wine", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), Name = "Speisen", Description = "Hauptgerichte und Vorspeisen", Color = "#e74c3c", Icon = "restaurant", SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), Name = "Desserts", Description = "Süße Nachspeisen und Kuchen", Color = "#f39c12", Icon = "ice-cream", SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), Name = "Snacks", Description = "Kleine Zwischenmahlzeiten", Color = "#27ae60", Icon = "fast-food", SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), Name = "Kaffee & Tee", Description = "Heiße Getränke", Color = "#8e44ad", Icon = "cafe", SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                };
                
                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
                Console.WriteLine($"{categories.Length} kategori eklendi.");
            }
            else
            {
                Console.WriteLine("Kategoriler zaten mevcut, atlanıyor...");
            }
            
            // Ürünler ekle
            if (!await context.Products.AnyAsync())
            {
                var products = new List<Product>();
                
                // Getränke - Standard Tax (20%)
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Cola 0.33L", Description = "Erfrischendes Cola-Getränk", Price = 2.50m, TaxType = 1, Category = "Getränke", IsActive = true, StockQuantity = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-COLA-033" },
                    new Product { Id = Guid.NewGuid(), Name = "Bier 0.5L", Description = "Helles Lagerbier", Price = 4.80m, TaxType = 1, Category = "Getränke", IsActive = true, StockQuantity = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-BIER-05" },
                    new Product { Id = Guid.NewGuid(), Name = "Wein 0.2L", Description = "Rotwein aus Österreich", Price = 6.50m, TaxType = 1, Category = "Getränke", IsActive = true, StockQuantity = 30, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-WEIN-02" },
                    new Product { Id = Guid.NewGuid(), Name = "Mineralwasser 0.5L", Description = "Natürliches Mineralwasser", Price = 1.80m, TaxType = 1, Category = "Getränke", IsActive = true, StockQuantity = 80, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-MINERAL-05" }
                });
                
                // Speisen - Standard Tax (20%)
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Wiener Schnitzel", Description = "Klassisches Wiener Schnitzel mit Pommes", Price = 18.90m, TaxType = 1, Category = "Speisen", IsActive = true, StockQuantity = 25, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-SCHNITZEL" },
                    new Product { Id = Guid.NewGuid(), Name = "Gulasch", Description = "Traditionelles Rindergulasch mit Semmelknödel", Price = 16.50m, TaxType = 1, Category = "Speisen", IsActive = true, StockQuantity = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-GULASCH" },
                    new Product { Id = Guid.NewGuid(), Name = "Salat", Description = "Gemischter Salat mit hausgemachtem Dressing", Price = 8.90m, TaxType = 1, Category = "Speisen", IsActive = true, StockQuantity = 15, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-SALAT" }
                });
                
                // Desserts - Reduced Tax (10%)
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Apfelstrudel", Description = "Hausgemachter Apfelstrudel mit Vanillesauce", Price = 6.90m, TaxType = 2, Category = "Desserts", IsActive = true, StockQuantity = 12, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-APFELSTRUDEL" },
                    new Product { Id = Guid.NewGuid(), Name = "Sachertorte", Description = "Original Wiener Sachertorte", Price = 7.50m, TaxType = 2, Category = "Desserts", IsActive = true, StockQuantity = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-SACHERTORTE" },
                    new Product { Id = Guid.NewGuid(), Name = "Eisbecher", Description = "3 Kugeln Eis mit Sahne und Schokoladensauce", Price = 5.90m, TaxType = 2, Category = "Desserts", IsActive = true, StockQuantity = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-EISBECHER" }
                });
                
                // Snacks - Reduced Tax (10%)
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Chips", Description = "Kartoffelchips mit Salz", Price = 3.50m, TaxType = 2, Category = "Snacks", IsActive = true, StockQuantity = 40, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-CHIPS" },
                    new Product { Id = Guid.NewGuid(), Name = "Nüsse", Description = "Gemischte Nüsse 100g", Price = 4.20m, TaxType = 2, Category = "Snacks", IsActive = true, StockQuantity = 25, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-NUESSE" },
                    new Product { Id = Guid.NewGuid(), Name = "Schokolade", Description = "Milchschokolade 100g", Price = 2.80m, TaxType = 2, Category = "Snacks", IsActive = true, StockQuantity = 35, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-SCHOKO" }
                });
                
                // Kaffee & Tee - Special Tax (13%)
                products.AddRange(new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Espresso", Description = "Starker italienischer Espresso", Price = 3.20m, TaxType = 3, Category = "Kaffee & Tee", IsActive = true, StockQuantity = 60, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-ESPRESSO" },
                    new Product { Id = Guid.NewGuid(), Name = "Cappuccino", Description = "Cappuccino mit Milchschaum", Price = 4.50m, TaxType = 3, Category = "Kaffee & Tee", IsActive = true, StockQuantity = 45, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-CAPPUCCINO" },
                    new Product { Id = Guid.NewGuid(), Name = "Tee", Description = "Kräutertee aus Österreich", Price = 3.80m, TaxType = 3, Category = "Kaffee & Tee", IsActive = true, StockQuantity = 30, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Barcode = "A-TEE" }
                });
                
                context.Products.AddRange(products);
                await context.SaveChangesAsync();
                
                Console.WriteLine($"{products.Count} ürün eklendi.");
                Console.WriteLine("Vergi grupları:");
                Console.WriteLine($"- Standard (20%): {products.Count(p => p.TaxType == 1)} ürün");
                Console.WriteLine($"- Reduced (10%): {products.Count(p => p.TaxType == 2)} ürün");
                Console.WriteLine($"- Special (13%): {products.Count(p => p.TaxType == 3)} ürün");
            }
            else
            {
                Console.WriteLine("Ürünler zaten mevcut, atlanıyor...");
            }
            
            Console.WriteLine("Demo veriler başarıyla eklendi!");
        }
    }
}
