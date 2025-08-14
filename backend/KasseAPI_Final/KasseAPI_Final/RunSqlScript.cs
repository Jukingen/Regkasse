using System;
using System.IO;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final
{
    public class RunSqlScript
    {
        public static async Task Main(string[] args)
        {
            // Servis sağlayıcıyı oluştur
            var serviceProvider = new ServiceCollection()
                .AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql("Host=localhost;Database=kassedb;Username=postgres;Password=postgres"))
                .BuildServiceProvider();

            // DbContext'i al
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                try
                {
                    // SQL dosyasını oku
                    string sqlScript = File.ReadAllText("create-user-settings-table.sql");
                    
                    // SQL komutunu çalıştır
                    await dbContext.Database.ExecuteSqlRawAsync(sqlScript);
                    
                    Console.WriteLine("SQL script başarıyla çalıştırıldı!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                }
            }
        }
    }
}
