using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Limits;

public sealed class TenantLimitAlertService : ITenantLimitAlertService
{
    /// <summary>
    /// <c>tenant_settings</c> key: one-shot gate for <see cref="ActivityEventType.OfflineQueueApproachingLimit"/>.
    /// Value <c>true</c> means the ≥80% crossing already published; cleared when usage drops below 80%.
    /// </summary>
    public const string OfflineQueueApproachingLimitEmittedKey = "Activity:OfflineQueueApproachingLimit.Emitted";

    private readonly ITenantLimitGuard _guard;
    private readonly IActivityEventPublisher _activity;
    private readonly AppDbContext _db;
    private readonly ILogger<TenantLimitAlertService> _logger;

    public TenantLimitAlertService(
        ITenantLimitGuard guard,
        IActivityEventPublisher activity,
        AppDbContext db,
        ILogger<TenantLimitAlertService> logger)
    {
        _guard = guard;
        _activity = activity;
        _db = db;
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
        await SyncOfflineQueueApproachingGateAsync(tenantId, rows, cancellationToken).ConfigureAwait(false);

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
            else if (row.Status == LimitUsageStatuses.Warning
                     && !string.Equals(row.Key, TenantLimitKeys.MaxOfflineTransactions, StringComparison.Ordinal))
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

    private async Task SyncOfflineQueueApproachingGateAsync(
        Guid tenantId,
        IReadOnlyList<LimitStatusDto> rows,
        CancellationToken cancellationToken)
    {
        var offline = rows.FirstOrDefault(r =>
            string.Equals(r.Key, TenantLimitKeys.MaxOfflineTransactions, StringComparison.Ordinal));
        if (offline is null)
        {
            return;
        }

        var atOrAboveWarning = offline.Percentage >= 80;
        if (!atOrAboveWarning)
        {
            await ClearEmittedFlagAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await TryMarkEmittedAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await PublishAsync(
                tenantId,
                ActivityEventType.OfflineQueueApproachingLimit,
                offline.Key,
                offline.Limit,
                offline.Current,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryMarkEmittedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await FindEmittedRowAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (row is not null && IsTrue(row.Value))
        {
            return false;
        }

        if (row is null)
        {
            _db.TenantSettings.Add(new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = OfflineQueueApproachingLimitEmittedKey,
                Value = "true",
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            row.Value = "true";
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ClearEmittedFlagAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await FindEmittedRowAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        if (IsTrue(row.Value))
        {
            row.Value = "false";
            row.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<TenantSetting?> FindEmittedRowAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.TenantSettings
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.Key == OfflineQueueApproachingLimitEmittedKey,
                cancellationToken);

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.Ordinal);

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
