using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Retries stored marketplace rows. No separate feature flag: a location with
/// <c>enabled = false</c> is not selected. In Production each row is skipped by its
/// provider confirmation constant. The cycle itself still runs.
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
        await MarkFoodoraAckDeadlinesAsync(ct);
        var dueIds = await ListDueIdsAsync(ct);
        if (dueIds.Count == 0)
            return;

        using var scope = _scopes.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var processor = scope.ServiceProvider.GetRequiredService<IOnlineOrderIntegrationProcessor>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;

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

    internal async Task<IReadOnlyList<Guid>> ListDueIdsAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var production = _environment.IsProduction();
        var woltConfirmed = OnlineOrderIntegrationSignatureStatus.IsConfirmed(OnlineOrderIntegrationSignatureStatus.WoltProvider);
        var foodoraConfirmed = OnlineOrderIntegrationSignatureStatus.IsConfirmed(OnlineOrderIntegrationSignatureStatus.FoodoraProvider);
        var dueIds = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .Where(r => r.IsActive
                        && r.AttemptCount < OnlineOrderIntegrationStatuses.MaxAttempts
                        && (r.Status == OnlineOrderIntegrationStatuses.Received
                            || r.Status == OnlineOrderIntegrationStatuses.Mapped)
                        && (r.NextAttemptAt == null || r.NextAttemptAt <= now)
                        && (!production
                            || (woltConfirmed && r.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider)
                            || (foodoraConfirmed && r.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider)))
            .OrderBy(r => r.NextAttemptAt)
            .Select(r => r.Id)
            .Take(20)
            .ToListAsync(ct);
        return dueIds;
    }

    private async Task MarkFoodoraAckDeadlinesAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<FoodoraAckDeadlineScanner>();
        await scanner.MarkExpiredAsync(ct);
    }
}
