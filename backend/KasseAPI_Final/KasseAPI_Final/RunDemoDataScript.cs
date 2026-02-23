using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final
{
    public class RunDemoDataScript
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== Demo Veri Ekleme Programı ===");
                Console.WriteLine("SQL script çalıştırılıyor...");
                
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=Juke1034#");
                
                using var context = new AppDbContext(optionsBuilder.Options);
                
                // SQL script dosyasını oku
                var sqlScript = await File.ReadAllTextAsync("demo-data.sql");
                
                Console.WriteLine("SQL script okundu, veritabanına uygulanıyor...");
                
                // SQL script'i çalıştır
                await context.Database.ExecuteSqlRawAsync(sqlScript);
                
                Console.WriteLine("SQL script başarıyla çalıştırıldı!");
                
                // Sonuçları kontrol et
                var categoryCount = await context.Categories.CountAsync();
                var productCount = await context.Products.CountAsync();
                
                Console.WriteLine($"\nEklenen veriler:");
                Console.WriteLine($"- Kategoriler: {categoryCount}");
                Console.WriteLine($"- Ürünler: {productCount}");
                
                // Vergi gruplarına göre ürün sayıları
                var standardTaxCount = await context.Products.CountAsync(p => p.TaxType == 1);
                var reducedTaxCount = await context.Products.CountAsync(p => p.TaxType == 2);
                var specialTaxCount = await context.Products.CountAsync(p => p.TaxType == 3);
                
                Console.WriteLine($"\nVergi grupları:");
                Console.WriteLine($"- Standard (20%): {standardTaxCount} ürün");
                Console.WriteLine($"- Reduced (10%): {reducedTaxCount} ürün");
                Console.WriteLine($"- Special (13%): {specialTaxCount} ürün");
                
                Console.WriteLine("\nDemo veriler başarıyla eklendi!");
                Console.WriteLine("Şimdi frontend'de ürünleri görebilir ve test edebilirsiniz.");
                Console.WriteLine("\nBasılan tuş: ");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata oluştu: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.WriteLine("\nBasılan tuş: ");
                Console.ReadKey();
            }
        }
    }
}
