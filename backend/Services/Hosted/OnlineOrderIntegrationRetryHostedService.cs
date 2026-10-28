using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Retries stored marketplace rows. No separate feature flag: a location with
/// <c>enabled = false</c> is not selected. In Production the unconfirmed signature
/// constant skips the cycle entirely.
/// </summary>
public sealed class OnlineOrderIntegrationRetryHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlineOrderIntegrationRetryHostedService> _logger;

    public OnlineOrderIntegrationRetryHostedService(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        TimeProvider time,
        ILogger<OnlineOrderIntegrationRetryHostedService> logger)
    {
        _scopes = scopes;
        _environment = environment;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_environment.IsProduction() || OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed)
                    await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Online-order integration retry cycle failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task PollAsync(CancellationToken ct)
    {
        if (_environment.IsProduction() && !OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed)
            return;

        using var scope = _scopes.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var processor = scope.ServiceProvider.GetRequiredService<IOnlineOrderIntegrationProcessor>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var dueIds = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .Where(r => r.IsActive
                        && r.AttemptCount < OnlineOrderIntegrationStatuses.MaxAttempts
                        && (r.Status == OnlineOrderIntegrationStatuses.Received
                            || r.Status == OnlineOrderIntegrationStatuses.Mapped)
                        && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
            .OrderBy(r => r.NextAttemptAt)
            .Select(r => r.Id)
            .Take(20)
            .ToListAsync(ct);

        foreach (var id in dueIds)
        {
            var claimed = await db.OnlineOrderIntegrations
                .IgnoreQueryFilters()
                .Where(r => r.Id == id
                            && r.IsActive
                            && r.AttemptCount < OnlineOrderIntegrationStatuses.MaxAttempts
                            && (r.Status == OnlineOrderIntegrationStatuses.Received
                                || r.Status == OnlineOrderIntegrationStatuses.Mapped)
                            && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(r => r.NextAttemptAt, now.AddMinutes(2)),
                    ct);
            if (claimed == 0)
                continue;

            await processor.ProcessAsync(id, ct);
        }
    }
}
