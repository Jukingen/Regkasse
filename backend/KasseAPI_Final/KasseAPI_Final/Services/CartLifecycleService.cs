using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Akıllı Sepet Yaşam Döngüsü Yönetimi
    /// - Otomatik sepet temizleme (session expire)
    /// - Kullanıcıya özel sepet yönetimi
    /// - Database temizliği
    /// </summary>
    public class CartLifecycleService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CartLifecycleService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // 1 dakikada bir kontrol (15 dakika + gece 00:00 için)

        public CartLifecycleService(
            IServiceProvider serviceProvider,
            ILogger<CartLifecycleService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 CartLifecycleService başlatıldı - Akıllı sepet yönetimi aktif");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredCarts();
                    await CleanupOrphanedCarts();
                    await CleanupCartsByTimeRules(); // 15 dakika + gece 00:00 kontrolü
                    await LogCartStatistics();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ CartLifecycleService hatası");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        /// <summary>
        /// Süresi dolmuş sepetleri temizle
        /// </summary>
        private async Task CleanupExpiredCarts()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // 🔒 GÜVENLİK: Sadece süresi dolmuş aktif sepetleri bul
                var expiredCarts = await context.Carts
                    .Include(c => c.Items)
                    .Where(c => c.ExpiresAt < DateTime.UtcNow && 
                               c.Status == CartStatus.Active)
                    .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count })
                    .ToListAsync();

                if (expiredCarts.Any())
                {
                    _logger.LogInformation("🧹 {ExpiredCartsCount} süresi dolmuş sepet bulundu, temizleniyor...", 
                        expiredCarts.Count);
                    
                    // Batch delete için ID'leri topla
                    var cartIds = expiredCarts.Select(c => c.CartId).ToList();
                    
                    // Önce CartItems'ları sil
                    var cartItemsToDelete = await context.CartItems
                        .Where(ci => cartIds.Contains(ci.CartId))
                        .ToListAsync();
                    
                    if (cartItemsToDelete.Any())
                    {
                        context.CartItems.RemoveRange(cartItemsToDelete);
                        _logger.LogInformation("🧹 {ItemsCount} sepet ürünü silindi", cartItemsToDelete.Count);
                    }
                    
                    // Sonra Cart'ları sil
                    var cartsToDelete = await context.Carts
                        .Where(c => cartIds.Contains(c.CartId))
                        .ToListAsync();
                    
                    context.Carts.RemoveRange(cartsToDelete);
                    
                    // Değişiklikleri kaydet
                    var deletedCount = await context.SaveChangesAsync();
                    
                    _logger.LogInformation("✅ {ExpiredCartsCount} süresi dolmuş sepet temizlendi, {DeletedCount} kayıt silindi", 
                        expiredCarts.Count, deletedCount);
                    
                    // Güvenlik log'u
                    foreach (var cart in expiredCarts)
                    {
                        _logger.LogInformation("🗑️ Expired cart cleaned: CartId={CartId}, UserId={UserId}, TableNumber={TableNumber}, ItemsCount={ItemsCount}", 
                            cart.CartId, cart.UserId, cart.TableNumber, cart.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Süresi dolmuş sepet temizleme hatası");
            }
        }

        /// <summary>
        /// Kullanıcısı olmayan (orphaned) sepetleri temizle
        /// </summary>
        private async Task CleanupOrphanedCarts()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // 🔒 GÜVENLİK: UserId olmayan veya geçersiz UserId'li sepetleri bul
                var orphanedCarts = await context.Carts
                    .Include(c => c.Items)
                    .Where(c => string.IsNullOrEmpty(c.UserId) || 
                               !context.Users.Any(u => u.Id == c.UserId))
                    .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count })
                    .ToListAsync();

                if (orphanedCarts.Any())
                {
                    _logger.LogWarning("⚠️ {OrphanedCartsCount} kullanıcısız sepet bulundu, güvenlik için temizleniyor...", 
                        orphanedCarts.Count);
                    
                    // Batch delete için ID'leri topla
                    var cartIds = orphanedCarts.Select(c => c.CartId).ToList();
                    
                    // Önce CartItems'ları sil
                    var cartItemsToDelete = await context.CartItems
                        .Where(ci => cartIds.Contains(ci.CartId))
                        .ToListAsync();
                    
                    if (cartItemsToDelete.Any())
                    {
                        context.CartItems.RemoveRange(cartItemsToDelete);
                        _logger.LogInformation("🧹 {ItemsCount} kullanıcısız sepet ürünü silindi", cartItemsToDelete.Count);
                    }
                    
                    // Sonra Cart'ları sil
                    var cartsToDelete = await context.Carts
                        .Where(c => cartIds.Contains(c.CartId))
                        .ToListAsync();
                    
                    context.Carts.RemoveRange(cartsToDelete);
                    
                    // Değişiklikleri kaydet
                    var deletedCount = await context.SaveChangesAsync();
                    
                    _logger.LogInformation("✅ {OrphanedCartsCount} kullanıcısız sepet temizlendi, {DeletedCount} kayıt silindi", 
                        orphanedCarts.Count, deletedCount);
                    
                    // Güvenlik log'u
                    foreach (var cart in orphanedCarts)
                    {
                        _logger.LogWarning("⚠️ Orphaned cart cleaned: CartId={CartId}, UserId={UserId}, TableNumber={TableNumber}, ItemsCount={ItemsCount}", 
                            cart.CartId, cart.UserId ?? "NULL", cart.TableNumber, cart.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Kullanıcısız sepet temizleme hatası");
            }
        }

        /// <summary>
        /// Zaman kurallarına göre sepet temizleme (15 dakika + gece 00:00)
        /// </summary>
        private async Task CleanupCartsByTimeRules()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var now = DateTime.UtcNow;
                var currentHour = now.Hour;
                var currentMinute = now.Minute;

                // 🌙 Gece 00:00 kontrolü - Tüm aktif sepetleri temizle
                if (currentHour == 0 && currentMinute == 0)
                {
                    _logger.LogInformation("🌙 Gece 00:00 - Tüm aktif sepetler otomatik sıfırlanıyor...");
                    
                    var allActiveCarts = await context.Carts
                        .Include(c => c.Items)
                        .Where(c => c.Status == CartStatus.Active)
                        .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count })
                        .ToListAsync();

                    if (allActiveCarts.Any())
                    {
                        _logger.LogInformation("🧹 {ActiveCartsCount} aktif sepet bulundu, gece 00:00 temizliği yapılıyor...", 
                            allActiveCarts.Count);

                        // Önce CartItems'ları sil
                        var cartItemsToDelete = await context.CartItems
                            .Where(ci => context.Carts
                                .Where(c => c.Status == CartStatus.Active)
                                .Select(c => c.CartId)
                                .Contains(ci.CartId))
                            .ToListAsync();

                        if (cartItemsToDelete.Any())
                        {
                            context.CartItems.RemoveRange(cartItemsToDelete);
                            _logger.LogInformation("🧹 {ItemsCount} sepet ürünü silindi (gece 00:00)", cartItemsToDelete.Count);
                        }

                        // Sonra Cart'ları sil
                        var cartsToDelete = await context.Carts
                            .Where(c => c.Status == CartStatus.Active)
                            .ToListAsync();

                        context.Carts.RemoveRange(cartsToDelete);

                        // Değişiklikleri kaydet
                        var deletedCount = await context.SaveChangesAsync();
                        
                        _logger.LogInformation("✅ Gece 00:00 temizliği tamamlandı: {CartsCount} sepet, {ItemsCount} ürün silindi", 
                            cartsToDelete.Count, cartItemsToDelete.Count);
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ Gece 00:00 - Temizlenecek aktif sepet bulunamadı");
                    }
                    
                    return; // Gece 00:00 işlemi yapıldıysa diğer kontrolleri atla
                }

                // ⏰ 15 dakika kontrolü - Her sepet için oluşturulma zamanından 15 dakika geçtiyse temizle
                var fifteenMinutesAgo = now.AddMinutes(-15);
                
                var expiredByTimeRule = await context.Carts
                    .Include(c => c.Items)
                    .Where(c => c.Status == CartStatus.Active && 
                               c.CreatedAt < fifteenMinutesAgo)
                    .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count, c.CreatedAt })
                    .ToListAsync();

                if (expiredByTimeRule.Any())
                {
                    _logger.LogInformation("⏰ {ExpiredCartsCount} sepet 15 dakika kuralına göre süresi doldu, temizleniyor...", 
                        expiredByTimeRule.Count);

                    // Batch delete için ID'leri topla
                    var cartIds = expiredByTimeRule.Select(c => c.CartId).ToList();

                    // Önce CartItems'ları sil
                    var cartItemsToDelete = await context.CartItems
                        .Where(ci => cartIds.Contains(ci.CartId))
                        .ToListAsync();

                    if (cartItemsToDelete.Any())
                    {
                        context.CartItems.RemoveRange(cartItemsToDelete);
                        _logger.LogInformation("🧹 {ItemsCount} sepet ürünü silindi (15 dakika kuralı)", cartItemsToDelete.Count);
                    }

                    // Sonra Cart'ları sil
                    var cartsToDelete = await context.Carts
                        .Where(c => cartIds.Contains(c.CartId))
                        .ToListAsync();

                    context.Carts.RemoveRange(cartsToDelete);

                    // Değişiklikleri kaydet
                    var deletedCount = await context.SaveChangesAsync();

                    _logger.LogInformation("✅ 15 dakika kuralı temizliği tamamlandı: {CartsCount} sepet, {ItemsCount} ürün silindi", 
                        cartsToDelete.Count, cartItemsToDelete.Count);

                    // Detaylı log
                    foreach (var cart in expiredByTimeRule)
                    {
                        var timeDiff = now - cart.CreatedAt;
                        var minutesDiff = (int)timeDiff.TotalMinutes;
                        _logger.LogInformation("⏰ Masa {TableNumber} sepeti {MinutesDiff} dakika geçti, sıfırlandı (UserId: {UserId})", 
                            cart.TableNumber, minutesDiff, cart.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CleanupCartsByTimeRules hatası");
            }
        }

        /// <summary>
        /// Sepet istatistiklerini logla
        /// </summary>
        private async Task LogCartStatistics()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var stats = await context.Carts
                    .GroupBy(c => c.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var totalItems = await context.CartItems.CountAsync();
                var activeUsers = await context.Carts
                    .Where(c => c.Status == CartStatus.Active)
                    .Select(c => c.UserId)
                    .Distinct()
                    .CountAsync();

                _logger.LogInformation("📊 Sepet İstatistikleri - Aktif: {Active}, Tamamlanan: {Completed}, Toplam Ürün: {TotalItems}, Aktif Kullanıcı: {ActiveUsers}", 
                    stats.FirstOrDefault(s => s.Status == CartStatus.Active)?.Count ?? 0,
                    stats.FirstOrDefault(s => s.Status == CartStatus.Completed)?.Count ?? 0,
                    totalItems,
                    activeUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Sepet istatistikleri loglama hatası");
            }
        }

        /// <summary>
        /// Manuel sepet temizleme (logout sırasında çağrılır)
        /// </summary>
        public async Task CleanupUserCarts(string userId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                _logger.LogInformation("🧹 Kullanıcı {UserId} için sepet temizleme başlatıldı", userId);
                
                // Transaction kullanarak güvenli silme işlemi
                using var transaction = await context.Database.BeginTransactionAsync();
                
                try
                {
                    // 🔒 GÜVENLİK: Sadece belirtilen kullanıcının aktif sepetlerini bul
                    var userCarts = await context.Carts
                        .Include(c => c.Items)
                        .Where(c => c.UserId == userId && 
                                   c.Status == CartStatus.Active)
                        .Select(c => new { c.CartId, c.TableNumber, c.Items.Count })
                        .ToListAsync();

                    if (userCarts.Any())
                    {
                        _logger.LogInformation("🧹 Kullanıcı {UserId} için {CartsCount} aktif sepet bulundu, temizleniyor...", 
                            userId, userCarts.Count);
                        
                        // Batch delete için ID'leri topla
                        var cartIds = userCarts.Select(c => c.CartId).ToList();
                        
                        // Önce CartItems'ları sil
                        var cartItemsToDelete = await context.CartItems
                            .Where(ci => cartIds.Contains(ci.CartId))
                            .ToListAsync();
                        
                        if (cartItemsToDelete.Any())
                        {
                            context.CartItems.RemoveRange(cartItemsToDelete);
                            _logger.LogInformation("🧹 Kullanıcı {UserId} için {ItemsCount} sepet ürünü silindi", 
                                cartItemsToDelete.Count);
                        }
                        
                        // Sonra Cart'ları sil
                        var cartsToDelete = await context.Carts
                            .Where(c => cartIds.Contains(c.CartId))
                            .ToListAsync();
                        
                        context.Carts.RemoveRange(cartsToDelete);
                        
                        // Değişiklikleri kaydet
                        var deletedCount = await context.SaveChangesAsync();
                        
                        // Transaction'ı commit et
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation("✅ Kullanıcı {UserId} için {CartsCount} sepet temizlendi, {DeletedCount} kayıt silindi", 
                            userCarts.Count, deletedCount);
                        
                        // Güvenlik log'u
                        foreach (var cart in userCarts)
                        {
                            _logger.LogInformation("🗑️ User cart cleaned: CartId={CartId}, UserId={UserId}, TableNumber={TableNumber}, ItemsCount={ItemsCount}", 
                                cart.CartId, userId, cart.TableNumber, cart.Count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ Kullanıcı {UserId} için aktif sepet bulunamadı", userId);
                        // Transaction'ı commit et (hiçbir değişiklik yok)
                        await transaction.CommitAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Hata durumunda transaction'ı rollback et
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "❌ Transaction hatası, rollback yapıldı: {UserId}", userId);
                    throw; // Hatayı yukarı fırlat
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Kullanıcı sepet temizleme hatası: {UserId}. Exception: {ExceptionType}, Message: {ExceptionMessage}", 
                    userId, ex.GetType().Name, ex.Message);
                throw; // Hatayı yukarı fırlat
            }
        }
    }
}
