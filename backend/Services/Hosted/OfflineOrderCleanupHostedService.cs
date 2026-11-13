using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Marks expired pending <see cref="Models.OfflineOrder"/> rows as expired and writes audit entries.</summary>
public sealed class OfflineOrderCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfflineOrderCleanupHostedService> _logger;

    public OfflineOrderCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfflineOrderCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                List<Guid> tenantIds;
                await using (var listScope = _scopeFactory.CreateAsyncScope())
                {
                    var listDb = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    tenantIds = await listDb.Tenants.AsNoTracking()
                        .Where(t => t.IsActive)
                        .Select(t => t.Id)
                        .ToListAsync(stoppingToken)
                        .ConfigureAwait(false);
                }

                var marked = 0;
                foreach (var tenantId in tenantIds)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                    tenantAccessor.TenantId = tenantId;
                    var offlineOrders = scope.ServiceProvider.GetRequiredService<IOfflineOrderService>();
                    marked += await offlineOrders.CleanupExpiredOrdersAsync(stoppingToken).ConfigureAwait(false);
                }

                if (marked > 0)
                    _logger.LogInformation("Offline order cleanup marked {Count} row(s) expired.", marked);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Offline order cleanup hosted service iteration failed.");
            }
        }
    }
}
