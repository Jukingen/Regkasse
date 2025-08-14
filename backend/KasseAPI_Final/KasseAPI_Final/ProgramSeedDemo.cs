using System;
using System.Threading.Tasks;

namespace KasseAPI_Final
{
    public class ProgramSeedDemo
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== KasseAPI Demo Veri Ekleme Programı ===");
                Console.WriteLine("Veritabanına demo kategoriler ve ürünler ekleniyor...");
                
                await SeedDemoData.SeedAsync();
                
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
