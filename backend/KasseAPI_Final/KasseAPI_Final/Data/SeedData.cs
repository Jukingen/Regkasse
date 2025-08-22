using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data
{
    public static class SeedData
    {
        public static async Task SeedProductsAsync(AppDbContext context)
        {
            // Eğer ürünler zaten varsa ekleme
            if (await context.Products.AnyAsync())
            {
                return;
            }

            var products = new List<Product>
            {
                new Product
                {
                    Name = "Wiener Schnitzel",
                    Price = 18.50m,
                    TaxType = "Standard",
                    Description = "Traditional Austrian breaded veal cutlet",
                    Category = "Hauptgerichte",
                    StockQuantity = 50,
                    MinStockLevel = 10,
                    Unit = "piece",
                    Cost = 12.00m,
                    TaxRate = 20.0m,
                    IsActive = true
                },
                new Product
                {
                    Name = "Goulash",
                    Price = 16.80m,
                    TaxType = "Standard",
                    Description = "Hungarian-style beef stew",
                    Category = "Hauptgerichte",
                    StockQuantity = 40,
                    MinStockLevel = 8,
                    Unit = "portion",
                    Cost = 10.50m,
                    TaxRate = 20.0m,
                    IsActive = true
                },
                new Product
                {
                    Name = "Beer - Gösser",
                    Price = 4.50m,
                    TaxType = "Special",
                    Description = "Austrian lager beer",
                    Category = "Alkoholische Getränke",
                    StockQuantity = 100,
                    MinStockLevel = 20,
                    Unit = "bottle",
                    Cost = 2.80m,
                    TaxRate = 13.0m,
                    IsActive = true
                },
                new Product
                {
                    Name = "Coffee - Melange",
                    Price = 3.80m,
                    TaxType = "Reduced",
                    Description = "Vienna coffee with milk",
                    Category = "Kaffee & Tee",
                    StockQuantity = 80,
                    MinStockLevel = 15,
                    Unit = "cup",
                    Cost = 1.20m,
                    TaxRate = 10.0m,
                    IsActive = true
                },
                new Product
                {
                    Name = "Apple Strudel",
                    Price = 6.50m,
                    TaxType = "Reduced",
                    Description = "Traditional Austrian apple pastry",
                    Category = "Desserts",
                    StockQuantity = 30,
                    MinStockLevel = 5,
                    Unit = "piece",
                    Cost = 3.20m,
                    TaxRate = 10.0m,
                    IsActive = true
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
}
