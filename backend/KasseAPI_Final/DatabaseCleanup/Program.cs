using System;
using System.Data;
using Npgsql;

namespace DatabaseCleanup
{
    class Program
    {
        static string connectionString = "Host=localhost;Database=kassedb;Username=postgres;Password=Juke";

        static void Main(string[] args)
        {
            Console.WriteLine("🗄️ Masa Bazlı Sepet Yönetimi Veritabanı Temizliği");
            Console.WriteLine("==================================================");
            Console.WriteLine();

            try
            {
                // 0. Mevcut tabloları listele
                Console.WriteLine("📋 Mevcut tablolar listeleniyor...");
                ListTables();

                // 1. Mevcut durumu kontrol et
                Console.WriteLine("📊 Mevcut durum kontrol ediliyor...");
                CheckCurrentStatus();

                // 2. Eski verileri temizle
                Console.WriteLine("🧹 Eski veriler temizleniyor...");
                CleanupOldData();

                // 3. Constraint'leri ekle
                Console.WriteLine("🔒 Constraint'ler ekleniyor...");
                AddConstraints();

                // 4. Index'leri ekle
                Console.WriteLine("📊 Index'ler ekleniyor...");
                AddIndexes();

                // 5. View oluştur
                Console.WriteLine("🎯 İstatistik view'ı oluşturuluyor...");
                CreateStatisticsView();

                // 6. Son durumu kontrol et
                Console.WriteLine("✅ Temizlik sonrası kontrol...");
                CheckFinalStatus();

                Console.WriteLine();
                Console.WriteLine("🎉 Veritabanı temizliği başarıyla tamamlandı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Çıkmak için herhangi bir tuşa basın...");
            Console.ReadKey();
        }

        static void ListTables()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                
                // Mevcut tabloları listele
                using (var command = new NpgsqlCommand(@"
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    ORDER BY table_name", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        Console.WriteLine("   📋 Mevcut tablolar:");
                        while (reader.Read())
                        {
                            var tableName = reader["table_name"];
                            Console.WriteLine($"      - {tableName}");
                        }
                    }
                }

                // Carts tablosunun yapısını kontrol et
                try
                {
                    using (var command = new NpgsqlCommand(@"
                        SELECT column_name, data_type, is_nullable
                        FROM information_schema.columns 
                        WHERE table_name = 'carts' 
                        ORDER BY ordinal_position", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            Console.WriteLine("   🔍 Carts tablosu yapısı:");
                            while (reader.Read())
                            {
                                var columnName = reader["column_name"];
                                var dataType = reader["data_type"];
                                var isNullable = reader["is_nullable"];
                                Console.WriteLine($"      - {columnName}: {dataType} (NULL: {isNullable})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ Carts tablosu yapısı alınamadı: {ex.Message}");
                }
            }
        }

        static void CheckCurrentStatus()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                
                try
                {
                    // TableNumber olmayan sepetleri say
                    using (var command = new NpgsqlCommand(@"
                        SELECT COUNT(*) as NullTableNumberCount
                        FROM ""carts"" 
                        WHERE ""TableNumber"" IS NULL", connection))
                    {
                        var count = command.ExecuteScalar();
                        Console.WriteLine($"   - TableNumber NULL olan sepetler: {count}");
                    }

                    // Geçersiz TableNumber'ları say
                    using (var command = new NpgsqlCommand(@"
                        SELECT COUNT(*) as InvalidTableNumberCount
                        FROM ""carts"" 
                        WHERE ""TableNumber"" <= 0 OR ""TableNumber"" > 100", connection))
                    {
                        var count = command.ExecuteScalar();
                        Console.WriteLine($"   - Geçersiz TableNumber'lar: {count}");
                    }

                    // Toplam sepet sayısı
                    using (var command = new NpgsqlCommand(@"
                        SELECT COUNT(*) as TotalCarts
                        FROM ""carts""", connection))
                    {
                        var count = command.ExecuteScalar();
                        Console.WriteLine($"   - Toplam sepet sayısı: {count}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ Durum kontrolü yapılamadı: {ex.Message}");
                }
            }
        }

        static void CleanupOldData()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Önce CartItems temizliği
                        using (var command = new NpgsqlCommand(@"
                            DELETE FROM ""cart_items"" 
                            WHERE ""CartId"" IN (
                                SELECT c.""CartId"" 
                                FROM ""carts"" c 
                                WHERE c.""TableNumber"" IS NULL 
                                   OR c.""TableNumber"" <= 0
                                   OR c.""TableNumber"" > 100
                            )", connection, transaction))
                        {
                            var deletedItems = command.ExecuteNonQuery();
                            Console.WriteLine($"   - {deletedItems} CartItem temizlendi");
                        }

                        // Sonra Carts temizliği
                        using (var command = new NpgsqlCommand(@"
                            DELETE FROM ""carts"" 
                            WHERE ""TableNumber"" IS NULL 
                               OR ""TableNumber"" <= 0
                               OR ""TableNumber"" > 100", connection, transaction))
                        {
                            var deletedCarts = command.ExecuteNonQuery();
                            Console.WriteLine($"   - {deletedCarts} Cart temizlendi");
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        static void AddConstraints()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // TableNumber NOT NULL yap
                        using (var command = new NpgsqlCommand(@"
                            ALTER TABLE ""carts"" ALTER COLUMN ""TableNumber"" SET NOT NULL", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine("   - TableNumber NOT NULL yapıldı");
                        }

                        // Check constraint ekle (eğer yoksa)
                        try
                        {
                            using (var command = new NpgsqlCommand(@"
                                ALTER TABLE ""carts"" ADD CONSTRAINT ""CK_carts_TableNumber_Range"" 
                                CHECK (""TableNumber"" >= 1 AND ""TableNumber"" <= 100)", connection, transaction))
                            {
                                command.ExecuteNonQuery();
                                Console.WriteLine("   - TableNumber range check constraint eklendi");
                            }
                        }
                        catch (PostgresException ex) when (ex.SqlState == "42710")
                        {
                            Console.WriteLine("   - TableNumber range check constraint zaten mevcut");
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        static void AddIndexes()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Composite index
                        using (var command = new NpgsqlCommand(@"
                            CREATE INDEX IF NOT EXISTS ""IX_carts_TableNumber_Status_UserId"" 
                            ON ""carts"" (""TableNumber"", ""Status"", ""UserId"")", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine("   - Composite index eklendi");
                        }

                        // Masa bazlı index
                        using (var command = new NpgsqlCommand(@"
                            CREATE INDEX IF NOT EXISTS ""IX_carts_TableNumber_Status"" 
                            ON ""carts"" (""TableNumber"", ""Status"")", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine("   - Masa bazlı index eklendi");
                        }

                        // Geçmiş index (created_at kullanarak)
                        using (var command = new NpgsqlCommand(@"
                            CREATE INDEX IF NOT EXISTS ""IX_carts_TableNumber_created_at"" 
                            ON ""carts"" (""TableNumber"", ""created_at"")", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine("   - Geçmiş index eklendi");
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        static void CreateStatisticsView()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(@"
                    CREATE OR REPLACE VIEW ""TableCartStatistics"" AS
                    SELECT 
                        c.""TableNumber"",
                        COUNT(CASE WHEN c.""Status"" = 1 THEN 1 END) as ActiveCarts,
                        COUNT(CASE WHEN c.""Status"" = 2 THEN 1 END) as CompletedCarts,
                        COUNT(CASE WHEN c.""Status"" = 3 THEN 1 END) as CancelledCarts,
                        COUNT(CASE WHEN c.""Status"" = 4 THEN 1 END) as ExpiredCarts,
                        SUM(CASE WHEN c.""Status"" = 1 THEN ci.""Quantity"" ELSE 0 END) as TotalActiveItems,
                        MAX(c.""created_at"") as LastCartActivity
                    FROM ""carts"" c
                    LEFT JOIN ""cart_items"" ci ON c.""CartId"" = ci.""CartId""
                    GROUP BY c.""TableNumber""
                    ORDER BY c.""TableNumber""", connection))
                {
                    command.ExecuteNonQuery();
                    Console.WriteLine("   - TableCartStatistics view oluşturuldu");
                }
            }
        }

        static void CheckFinalStatus()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                
                // Masa bazlı sepet durumu
                using (var command = new NpgsqlCommand(@"
                    SELECT 
                        ""TableNumber"",
                        COUNT(*) as TotalCarts,
                        COUNT(CASE WHEN ""Status"" = 1 THEN 1 END) as ActiveCarts
                    FROM ""carts""
                    GROUP BY ""TableNumber""
                    ORDER BY ""TableNumber""", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        Console.WriteLine("   📊 Masa bazlı sepet durumu:");
                        while (reader.Read())
                        {
                            var tableNumber = reader["TableNumber"];
                            var totalCarts = reader["TotalCarts"];
                            var activeCarts = reader["ActiveCarts"];
                            Console.WriteLine($"      Masa {tableNumber}: {totalCarts} toplam, {activeCarts} aktif");
                        }
                    }
                }

                // İstatistik view'ı test et
                using (var command = new NpgsqlCommand(@"
                    SELECT * FROM ""TableCartStatistics""", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        Console.WriteLine("   📈 İstatistik view test edildi");
                    }
                }
            }
        }
    }
}
