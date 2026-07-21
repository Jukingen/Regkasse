using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DataDeletion;

/// <summary>
/// Daily sweep: executes confirmed tenant data-deletion requests whose 7-day wait has elapsed.
/// Uses <see cref="IServiceScopeFactory"/> because this is a singleton hosted service.
/// </summary>
public sealed class AutoPurgeService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoPurgeService> _logger;

    public AutoPurgeService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup grace — avoid racing migrations on boot.
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
                await CheckAndExecutePurgesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "AutoPurgeService iteration failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task CheckAndExecutePurgesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var deletion = scope.ServiceProvider.GetRequiredService<IDataDeletionService>();
        var ids = await deletion.ListPurgeEligibleRequestIdsAsync(ct).ConfigureAwait(false);
        if (ids.Count == 0)
            return;

        _logger.LogInformation("AutoPurgeService found {Count} eligible deletion request(s).", ids.Count);

        foreach (var id in ids)
        {
            var result = await deletion
                .ExecutePurgeAsync(id, actorUserId: "system", TenantDataDeletionExecutedVia.Auto, ct)
                .ConfigureAwait(false);

            if (result.Succeeded)
            {
                _logger.LogWarning(
                    "Auto-purged tenant customer data. RequestId={RequestId}, TenantId={TenantId}",
                    result.RequestId,
                    result.TenantId);
            }
            else
            {
                _logger.LogInformation(
                    "Auto-purge skipped. RequestId={RequestId}, Code={Code}, Error={Error}",
                    id,
                    result.ErrorCode,
                    result.Error);
            }
        }
    }
}
