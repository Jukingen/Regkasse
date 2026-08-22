using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;

namespace KasseAPI_Final.Services.Limits;

public sealed class TenantLimitAlertService : ITenantLimitAlertService
{
    private readonly ITenantLimitGuard _guard;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TenantLimitAlertService> _logger;

    public TenantLimitAlertService(
        ITenantLimitGuard guard,
        IActivityEventPublisher activity,
        ILogger<TenantLimitAlertService> logger)
    {
        _guard = guard;
        _activity = activity;
        _logger = logger;
    }

    public async Task EvaluateAndPublishAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        TenantLimitUsageDto usage;
        try
        {
            usage = await _guard.GetUsageAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Tenant limit alert evaluation skipped TenantId={TenantId}", tenantId);
            return;
        }

        var rows = LimitDashboardMapper.FromUsage(usage, tenantName: null);
        foreach (var row in rows)
        {
            if (row.Status == LimitUsageStatuses.Critical)
            {
                await PublishAsync(
                        tenantId,
                        ActivityEventType.LimitExceeded,
                        row.Key,
                        row.Limit,
                        row.Current,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (row.Status == LimitUsageStatuses.Warning)
            {
                await PublishAsync(
                        tenantId,
                        ActivityEventType.LimitApproaching,
                        row.Key,
                        row.Limit,
                        row.Current,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public Task PublishExceededAsync(
        Guid tenantId,
        LimitExceededException exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return PublishAsync(
            tenantId,
            ActivityEventType.LimitExceeded,
            exception.LimitKey,
            exception.LimitAmount,
            exception.CurrentAmount,
            cancellationToken);
    }

    private Task PublishAsync(
        Guid tenantId,
        ActivityEventType type,
        string limitKey,
        decimal limit,
        decimal current,
        CancellationToken cancellationToken) =>
        _activity.TryPublishAsync(
            LimitDashboardMapper.ToPublishRequest(tenantId, type, limitKey, limit, current),
            cancellationToken);
}
