using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KasseAPI_Final
{
    public class UpdateDatabase
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("PaymentDetails tablosunu güncelleniyor...");
                
                // Connection string
                var connectionString = "Host=localhost;Database=kassedb;Username=postgres;Password=postgres";
                
                // DbContext oluştur
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseAppNpgsql(connectionString);
                
                using var context = new AppDbContext(optionsBuilder.Options);
                
                // SQL script'i oku
                var sqlScript = await File.ReadAllTextAsync("update-payment-details-simple.sql");
                
                // SQL'i çalıştır
                await context.Database.ExecuteSqlRawAsync(sqlScript);
                
                Console.WriteLine("✅ PaymentDetails tablosu başarıyla güncellendi!");
                Console.WriteLine("🎯 Frontend için gerekli tüm alanlar eklendi");
                Console.WriteLine("📋 Avusturya yasal gereksinimleri (RKSV) destekleniyor");
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: {ex.Message}");
                Console.WriteLine($"📝 Detay: {ex}");
                return 1;
            }
        }
    }
}
