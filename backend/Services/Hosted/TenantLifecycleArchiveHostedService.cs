using KasseAPI_Final.Services.Tenancy;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Daily sweep: Cancelled tenants past 30-day retention → Archived.
/// </summary>
public sealed class TenantLifecycleArchiveHostedService : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantLifecycleArchiveHostedService> _logger;

    public TenantLifecycleArchiveHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantLifecycleArchiveHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger first run slightly after process start.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
                var archived = await tenants
                    .ArchiveExpiredCancellationsAsync(Retention, actorUserId: "system", stoppingToken)
                    .ConfigureAwait(false);
                if (archived > 0)
                    _logger.LogInformation("Tenant lifecycle archive moved {Count} cancelled tenants to archived", archived);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Tenant lifecycle archive sweep failed");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
