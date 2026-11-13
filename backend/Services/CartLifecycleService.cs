using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

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
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // 1 dakikada bir kontrol (45 dakika + gece 00:00 için)

        public CartLifecycleService(
            IServiceProvider serviceProvider,
            ILogger<CartLifecycleService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 CartLifecycleService started - Smart cart management active");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredCarts();
                    await CleanupOrphanedCarts();
                    await CleanupCartsByTimeRules(); // 45 dakika + gece 00:00 kontrolü
                    await LogCartStatistics();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ CartLifecycleService error");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        /// <summary>
        /// 45-minute idle cleanup for one ambient tenant. Test seam for
        /// <see cref="CartTenantCleanup.DeleteIdleActiveCartsAsync"/>.
        /// </summary>
        internal static Task<int> CleanupFortyFiveMinuteCartsAsync(
            AppDbContext context,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            CartTenantCleanup.DeleteIdleActiveCartsAsync(context, utcNow, cancellationToken);

        /// <summary>
        /// Süresi dolmuş sepetleri temizle
        /// </summary>
        private async Task CleanupExpiredCarts()
        {
            try
            {
                await ForEachActiveTenantAsync(async context =>
                {
                    await DeleteExpiredCartsForAmbientTenantAsync(context).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Expired cart cleanup error");
            }
        }

        private async Task DeleteExpiredCartsForAmbientTenantAsync(AppDbContext context)
        {
            try
            {
                var userIds = await CartTenantCleanup.ActiveMemberUserIdsAsync(context).ConfigureAwait(false);
                if (userIds.Count == 0)
                    return;

                // 🔒 GÜVENLİK: Sadece süresi dolmuş aktif sepetleri bul (ambient tenant üyeleri)
                var expiredCarts = await context.Carts
                    .Include(c => c.Items)
                    .Where(c => c.ExpiresAt < DateTime.UtcNow &&
                               c.Status == CartStatus.Active &&
                               userIds.Contains(c.UserId))
                    .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count })
                    .ToListAsync();

                if (expiredCarts.Any())
                {
                    _logger.LogInformation("🧹 {ExpiredCartsCount} expired carts found, cleaning...",
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
                        _logger.LogInformation("🧹 {ItemsCount} cart items deleted", cartItemsToDelete.Count);
                    }

                    // Sonra Cart'ları sil
                    var cartsToDelete = await context.Carts
                        .Where(c => cartIds.Contains(c.CartId))
                        .ToListAsync();

                    context.Carts.RemoveRange(cartsToDelete);

                    // Değişiklikleri kaydet
                    var deletedCount = await context.SaveChangesAsync();

                    _logger.LogInformation("✅ {ExpiredCartsCount} expired carts cleaned, {DeletedCount} records deleted",
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
                _logger.LogError(ex, "❌ Expired cart cleanup error");
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
                    _logger.LogWarning("⚠️ {OrphanedCartsCount} orphaned carts found, cleaning for security...",
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
                        _logger.LogInformation("🧹 {ItemsCount} orphaned cart items deleted", cartItemsToDelete.Count);
                    }

                    // Sonra Cart'ları sil
                    var cartsToDelete = await context.Carts
                        .Where(c => cartIds.Contains(c.CartId))
                        .ToListAsync();

                    context.Carts.RemoveRange(cartsToDelete);

                    // Değişiklikleri kaydet
                    var deletedCount = await context.SaveChangesAsync();

                    _logger.LogInformation("✅ {OrphanedCartsCount} orphaned carts cleaned, {DeletedCount} records deleted",
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
                _logger.LogError(ex, "❌ Orphaned cart cleanup error");
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> once per active tenant with that tenant bound on the scope accessor.
        /// Membership queries then see only that tenant. Carts are filtered by those user ids.
        /// </summary>
        private async Task ForEachActiveTenantAsync(Func<AppDbContext, Task> action)
        {
            List<Guid> tenantIds;
            using (var listScope = _serviceProvider.CreateScope())
            {
                var listDb = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
                tenantIds = await listDb.Tenants.AsNoTracking()
                    .Where(t => t.IsActive)
                    .Select(t => t.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            foreach (var tenantId in tenantIds)
            {
                using var scope = _serviceProvider.CreateScope();
                var accessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                accessor.TenantId = tenantId;
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await action(context).ConfigureAwait(false);
            }
        }

        private async Task CleanupCartsByTimeRules()
        {
            try
            {
                await ForEachActiveTenantAsync(CleanupCartsByTimeRulesForAmbientTenantAsync).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CleanupCartsByTimeRules error");
            }
        }

        private async Task CleanupCartsByTimeRulesForAmbientTenantAsync(AppDbContext context)
        {
            try
            {
                var now = DateTime.UtcNow;
                var currentHour = now.Hour;
                var currentMinute = now.Minute;
                var userIds = await CartTenantCleanup.ActiveMemberUserIdsAsync(context).ConfigureAwait(false);

                // 🌙 Gece 00:00 kontrolü - Yalnızca bu mandantın aktif sepetlerini temizle
                if (currentHour == 0 && currentMinute == 0)
                {
                    _logger.LogInformation("🌙 Midnight 00:00 - Active carts for the ambient tenant resetting...");

                    if (userIds.Count == 0)
                        return;

                    var allActiveCarts = await context.Carts
                        .Include(c => c.Items)
                        .Where(c => c.Status == CartStatus.Active && userIds.Contains(c.UserId))
                        .Select(c => new { c.CartId, c.UserId, c.TableNumber, c.Items.Count })
                        .ToListAsync();

                    if (allActiveCarts.Any())
                    {
                        _logger.LogInformation("🧹 {ActiveCartsCount} active carts found, performing midnight cleanup...",
                            allActiveCarts.Count);

                        // Önce CartItems'ları sil
                        var activeCartIds = allActiveCarts.Select(c => c.CartId).ToList();
                        var cartItemsToDelete = await context.CartItems
                            .Where(ci => activeCartIds.Contains(ci.CartId))
                            .ToListAsync();

                        if (cartItemsToDelete.Any())
                        {
                            context.CartItems.RemoveRange(cartItemsToDelete);
                            _logger.LogInformation("🧹 {ItemsCount} cart items deleted (midnight cleanup)", cartItemsToDelete.Count);
                        }

                        // Sonra Cart'ları sil
                        var cartsToDelete = await context.Carts
                            .Where(c => activeCartIds.Contains(c.CartId))
                            .ToListAsync();

                        context.Carts.RemoveRange(cartsToDelete);

                        // Değişiklikleri kaydet
                        var deletedCount = await context.SaveChangesAsync();

                        _logger.LogInformation("✅ Midnight cleanup completed: {CartsCount} carts, {ItemsCount} items deleted",
                            cartsToDelete.Count, cartItemsToDelete.Count);
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ Midnight 00:00 - No active carts found to clean");
                    }

                    return; // Gece 00:00 işlemi yapıldıysa diğer kontrolleri atla
                }

                var deletedIdle = await CleanupFortyFiveMinuteCartsAsync(context, now).ConfigureAwait(false);
                if (deletedIdle > 0)
                {
                    _logger.LogInformation(
                        "⏰ {ExpiredCartsCount} carts expired by 45-minute idle rule (UpdatedAt) for the ambient tenant",
                        deletedIdle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CleanupCartsByTimeRules error");
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

                _logger.LogDebug(
                    "Cart statistics - Active: {Active}, Completed: {Completed}, Total Items: {TotalItems}, Active Users: {ActiveUsers}",
                    stats.FirstOrDefault(s => s.Status == CartStatus.Active)?.Count ?? 0,
                    stats.FirstOrDefault(s => s.Status == CartStatus.Completed)?.Count ?? 0,
                    totalItems,
                    activeUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cart statistics logging error");
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
                _logger.LogInformation("🧹 Cart cleanup started for user {UserId}", userId);

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
                        _logger.LogInformation("🧹 {CartsCount} active carts found for user {UserId}, cleaning...",
                            userCarts.Count, userId);

                        // Batch delete için ID'leri topla
                        var cartIds = userCarts.Select(c => c.CartId).ToList();

                        // Önce CartItems'ları sil
                        var cartItemsToDelete = await context.CartItems
                            .Where(ci => cartIds.Contains(ci.CartId))
                            .ToListAsync();

                        if (cartItemsToDelete.Any())
                        {
                            context.CartItems.RemoveRange(cartItemsToDelete);
                            _logger.LogInformation("🧹 {ItemsCount} cart items deleted for user {UserId}",
    cartItemsToDelete.Count, userId);
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

                        _logger.LogInformation("✅ {CartsCount} carts cleaned for user {UserId}, {DeletedCount} records deleted",
                            userCarts.Count, userId, deletedCount);

                        // Güvenlik log'u
                        foreach (var cart in userCarts)
                        {
                            _logger.LogInformation("🗑️ User cart cleaned: CartId={CartId}, UserId={UserId}, TableNumber={TableNumber}, ItemsCount={ItemsCount}",
                                cart.CartId, userId, cart.TableNumber, cart.Count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ No active carts found for user {UserId}", userId);
                        // Transaction'ı commit et (hiçbir değişiklik yok)
                        await transaction.CommitAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Hata durumunda transaction'ı rollback et
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "❌ Transaction error, rollback performed: {UserId}", userId);
                    throw; // Hatayı yukarı fırlat
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ User cart cleanup error: {UserId}. Exception: {ExceptionType}, Message: {ExceptionMessage}",
                    userId, ex.GetType().Name, ex.Message);
                throw; // Hatayı yukarı fırlat
            }
        }
    }
}
