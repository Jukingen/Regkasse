using Microsoft.EntityFrameworkCore.Migrations;

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Eski sepet verilerini temizle - UserId olmayan veya geçersiz UserId'li sepetleri sil
    /// </summary>
    public partial class CleanupOrphanedCarts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🧹 Eski sepet verilerini temizle
            migrationBuilder.Sql(@"
                -- Önce CartItems tablosundan eski verileri temizle
                DELETE FROM ""CartItems"" 
                WHERE ""CartId"" IN (
                    SELECT c.""CartId"" 
                    FROM ""Carts"" c 
                    WHERE c.""UserId"" IS NULL 
                       OR c.""UserId"" = '' 
                       OR c.""UserId"" NOT IN (
                           SELECT u.""Id"" FROM ""AspNetUsers"" u
                       )
                );
                
                -- Sonra Carts tablosundan eski verileri temizle
                DELETE FROM ""Carts"" 
                WHERE ""UserId"" IS NULL 
                   OR ""UserId"" = '' 
                   OR ""UserId"" NOT IN (
                       SELECT u.""Id"" FROM ""AspNetUsers"" u
                   );
                
                -- UserId alanını NOT NULL yap (eğer zaten değilse)
                ALTER TABLE ""Carts"" ALTER COLUMN ""UserId"" SET NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma işlemi yok - veri silme işlemi geri alınamaz
            migrationBuilder.Sql(@"
                -- NOT: Bu migration geri alınamaz çünkü veri silme işlemi yapıyor
                -- Eğer geri alınması gerekirse, yedek verilerden restore edilmeli
            ");
        }
    }
}
