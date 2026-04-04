using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final
{
    public static class AddDemoData
    {
        public static async Task AddDemoDataAsync(AppDbContext context)
        {
            Console.WriteLine("Demo veriler ekleniyor...");
            var tenantId = LegacyDefaultTenantIds.Primary;

            // Kategoriler ekle
            if (!await context.Categories.AnyAsync())
            {
                var categories = new[]
                {
                    new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Getränke", Description = "Alkoholfreie und alkoholische Getränke", Color = "#3498db", Icon = "wine", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Speisen", Description = "Hauptgerichte und Vorspeisen", Color = "#e74c3c", Icon = "restaurant", SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Desserts", Description = "Süße Nachspeisen und Kuchen", Color = "#f39c12", Icon = "ice-cream", SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Snacks", Description = "Kleine Zwischenmahlzeiten", Color = "#27ae60", Icon = "fast-food", SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kaffee & Tee", Description = "Heiße Getränke", Color = "#8e44ad", Icon = "cafe", SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
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
                var catByName = await context.Categories
                    .AsNoTracking()
                    .Where(c => c.TenantId == tenantId)
                    .ToDictionaryAsync(c => c.Name, c => c.Id);

                static Product P(
                    Guid tenant,
                    Guid categoryId,
                    string categoryName,
                    string name,
                    string description,
                    decimal price,
                    int taxType,
                    int stock,
                    string barcode)
                {
                    var tr = TaxTypes.GetTaxRate(taxType);
                    return new Product
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant,
                        Name = name,
                        Description = description,
                        Price = price,
                        TaxType = taxType,
                        Category = categoryName,
                        CategoryId = categoryId,
                        IsActive = true,
                        StockQuantity = stock,
                        MinStockLevel = 0,
                        Unit = "Stk",
                        Barcode = barcode,
                        Cost = 0,
                        TaxRate = tr,
                        IsFiscalCompliant = true,
                        IsTaxable = true,
                        RksvProductType = RksvProductTypes.Standard,
                        IsSellableAddOn = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "demo",
                        UpdatedBy = "demo"
                    };
                }

                var products = new List<Product>();

                var getränke = catByName["Getränke"];
                var speisen = catByName["Speisen"];
                var desserts = catByName["Desserts"];
                var snacks = catByName["Snacks"];
                var kaffee = catByName["Kaffee & Tee"];

                products.AddRange(new[]
                {
                    P(tenantId, getränke, "Getränke", "Cola 0.33L", "Erfrischendes Cola-Getränk", 2.50m, 1, 100, "A-COLA-033"),
                    P(tenantId, getränke, "Getränke", "Bier 0.5L", "Helles Lagerbier", 4.80m, 1, 50, "A-BIER-05"),
                    P(tenantId, getränke, "Getränke", "Wein 0.2L", "Rotwein aus Österreich", 6.50m, 1, 30, "A-WEIN-02"),
                    P(tenantId, getränke, "Getränke", "Mineralwasser 0.5L", "Natürliches Mineralwasser", 1.80m, 1, 80, "A-MINERAL-05")
                });

                products.AddRange(new[]
                {
                    P(tenantId, speisen, "Speisen", "Wiener Schnitzel", "Klassisches Wiener Schnitzel mit Pommes", 18.90m, 1, 25, "A-SCHNITZEL"),
                    P(tenantId, speisen, "Speisen", "Gulasch", "Traditionelles Rindergulasch mit Semmelknödel", 16.50m, 1, 20, "A-GULASCH"),
                    P(tenantId, speisen, "Speisen", "Salat", "Gemischter Salat mit hausgemachtem Dressing", 8.90m, 1, 15, "A-SALAT")
                });

                products.AddRange(new[]
                {
                    P(tenantId, desserts, "Desserts", "Apfelstrudel", "Hausgemachter Apfelstrudel mit Vanillesauce", 6.90m, 2, 12, "A-APFELSTRUDEL"),
                    P(tenantId, desserts, "Desserts", "Sachertorte", "Original Wiener Sachertorte", 7.50m, 2, 10, "A-SACHERTORTE"),
                    P(tenantId, desserts, "Desserts", "Eisbecher", "3 Kugeln Eis mit Sahne und Schokoladensauce", 5.90m, 2, 20, "A-EISBECHER")
                });

                products.AddRange(new[]
                {
                    P(tenantId, snacks, "Snacks", "Chips", "Kartoffelchips mit Salz", 3.50m, 2, 40, "A-CHIPS"),
                    P(tenantId, snacks, "Snacks", "Nüsse", "Gemischte Nüsse 100g", 4.20m, 2, 25, "A-NUESSE"),
                    P(tenantId, snacks, "Snacks", "Schokolade", "Milchschokolade 100g", 2.80m, 2, 35, "A-SCHOKO")
                });

                products.AddRange(new[]
                {
                    P(tenantId, kaffee, "Kaffee & Tee", "Espresso", "Starker italienischer Espresso", 3.20m, 3, 60, "A-ESPRESSO"),
                    P(tenantId, kaffee, "Kaffee & Tee", "Cappuccino", "Cappuccino mit Milchschaum", 4.50m, 3, 45, "A-CAPPUCCINO"),
                    P(tenantId, kaffee, "Kaffee & Tee", "Tee", "Kräutertee aus Österreich", 3.80m, 3, 30, "A-TEE")
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
