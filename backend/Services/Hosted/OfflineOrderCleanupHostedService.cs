using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Deletes expired pending <see cref="Models.OfflineOrder"/> rows and writes audit entries.</summary>
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
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                tenantAccessor.TenantId = null;

                var offlineOrders = scope.ServiceProvider.GetRequiredService<IOfflineOrderService>();
                var deleted = await offlineOrders.CleanupExpiredOrdersAsync(stoppingToken).ConfigureAwait(false);

                if (deleted > 0)
                    _logger.LogInformation("Offline order cleanup removed {Count} expired row(s).", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Offline order cleanup hosted service iteration failed.");
            }
        }
    }
}
