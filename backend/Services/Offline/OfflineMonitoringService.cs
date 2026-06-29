using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Offline;

public sealed class OfflineMonitoringService : IOfflineMonitoringService
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IOptionsMonitor<OfflineMonitoringOptions> _options;
    private readonly IOptionsMonitor<OfflineAlertRules> _alertRules;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IHostEnvironment _environment;

    public OfflineMonitoringService(
        AppDbContext context,
        ICurrentTenantAccessor tenantAccessor,
        IOptionsMonitor<OfflineMonitoringOptions> options,
        IOptionsMonitor<OfflineAlertRules> alertRules,
        IOptionsMonitor<TseOptions> tseOptions,
        IHostEnvironment environment)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
        _options = options;
        _alertRules = alertRules;
        _tseOptions = tseOptions;
        _environment = environment;
    }

    public async Task<OfflineSystemStatus> GetSystemStatusAsync(CancellationToken ct = default)
    {
        var orderStats = await GetOrderStatsAsync(ct).ConfigureAwait(false);
        var transactionStats = await GetTransactionStatsAsync(ct).ConfigureAwait(false);
        var syncHealth = await GetSyncHealthAsync(ct).ConfigureAwait(false);
        var anomalies = await CheckAnomaliesAsync(ct).ConfigureAwait(false);
        var detail = await LoadOrderMonitoringDetailAsync(ct).ConfigureAwait(false);

        var lastOrderSync = await _context.OfflineOrders.AsNoTracking()
            .Where(o => o.Status == OfflineOrderStatuses.Synced && o.SyncedAtUtc != null)
            .MaxAsync(o => (DateTime?)o.SyncedAtUtc, ct)
            .ConfigureAwait(false);

        return new OfflineSystemStatus
        {
            TotalPendingOrders = orderStats.Pending,
            TotalPendingTransactions = transactionStats.PendingCount + transactionStats.NonFiscalPendingCount,
            TotalExpiredOrders = orderStats.Expired,
            TotalFailedSyncs = orderStats.Failed + transactionStats.FailedCount,
            OldestPendingOrder = detail.OldestPendingCreatedAtUtc,
            LastSyncAt = MaxUtc(lastOrderSync, transactionStats.LastReplayAtUtc),
            HasCriticalIssues = anomalies.Any(a => a.Severity == "critical") || !syncHealth.IsHealthy,
        };
    }

    public async Task<OfflineOrderStats> GetOrderStatsAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var now = DateTime.UtcNow;
        var baseQuery = _context.OfflineOrders.AsNoTracking();

        return new OfflineOrderStats
        {
            Total = await baseQuery.CountAsync(ct).ConfigureAwait(false),
            Pending = await baseQuery.CountAsync(
                o => o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc > now,
                ct).ConfigureAwait(false),
            Synced = await baseQuery.CountAsync(o => o.Status == OfflineOrderStatuses.Synced, ct)
                .ConfigureAwait(false),
            Failed = await baseQuery.CountAsync(o => o.Status == OfflineOrderStatuses.Failed, ct)
                .ConfigureAwait(false),
            Expired = await baseQuery.CountAsync(
                o => o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc <= now,
                ct).ConfigureAwait(false),
        };
    }

    public async Task<OfflineTransactionStats> GetTransactionStatsAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var now = DateTime.UtcNow;
        var since24h = now.AddHours(-24);

        var baseQuery = _context.OfflineTransactions.AsNoTracking();

        var pendingCount = await baseQuery.CountAsync(
            x => x.Status == OfflineTransactionStatus.Pending, ct).ConfigureAwait(false);
        var nonFiscalPending = await baseQuery.CountAsync(
            x => x.Status == OfflineTransactionStatus.NonFiscalPending, ct).ConfigureAwait(false);
        var failedCount = await baseQuery.CountAsync(
            x => x.Status == OfflineTransactionStatus.Failed, ct).ConfigureAwait(false);
        var syncedLast24h = await baseQuery.CountAsync(
            x => x.Status == OfflineTransactionStatus.Synced
                 && x.FiscalizedAtUtc != null
                 && x.FiscalizedAtUtc >= since24h,
            ct).ConfigureAwait(false);
        var clockDrift = await baseQuery.CountAsync(x => x.ClockDriftWarning, ct).ConfigureAwait(false);
        var sequenceGap = await baseQuery.CountAsync(x => x.SequenceGapDetected, ct).ConfigureAwait(false);
        var lastReplay = await baseQuery
            .Where(x => x.LastReplayAttemptAt != null)
            .MaxAsync(x => (DateTime?)x.LastReplayAttemptAt, ct)
            .ConfigureAwait(false);

        var byRegister = await BuildTransactionRegisterBreakdownAsync(ct).ConfigureAwait(false);

        return new OfflineTransactionStats
        {
            PendingCount = pendingCount,
            NonFiscalPendingCount = nonFiscalPending,
            FailedCount = failedCount,
            SyncedLast24Hours = syncedLast24h,
            ClockDriftWarningCount = clockDrift,
            SequenceGapCount = sequenceGap,
            LastReplayAtUtc = lastReplay,
            ByRegister = byRegister,
        };
    }

    public async Task<List<OfflineAnomaly>> CheckAnomaliesAsync(CancellationToken ct = default)
    {
        RequireTenantId();
        var opts = _options.CurrentValue;
        var rules = _alertRules.CurrentValue;
        var detectedAt = DateTime.UtcNow;
        var anomalies = new List<OfflineAnomaly>();

        var orderStats = await GetOrderStatsAsync(ct).ConfigureAwait(false);
        var orderDetail = await LoadOrderMonitoringDetailAsync(ct).ConfigureAwait(false);
        var transactionStats = await GetTransactionStatsAsync(ct).ConfigureAwait(false);
        var syncDetail = await LoadSyncHealthDetailAsync(ct).ConfigureAwait(false);
        var syncHealth = await GetSyncHealthAsync(ct).ConfigureAwait(false);
        var registerLabels = await LoadRegisterLabelsAsync(ct).ConfigureAwait(false);

        if (orderStats.Pending > rules.MaxPendingOrders)
        {
            anomalies.Add(Anomaly(
                "too_many_pending",
                "critical",
                $"{orderStats.Pending} pending offline orders exceed tenant limit ({rules.MaxPendingOrders}).",
                detectedAt));
        }

        if (orderDetail.PendingOlderThanHours > 0)
        {
            anomalies.Add(Anomaly(
                "old_pending",
                "warning",
                $"{orderDetail.PendingOlderThanHours} pending offline order(s) older than {rules.MaxPendingAgeHours} hours.",
                detectedAt));
        }

        if (orderStats.Expired > 0)
        {
            anomalies.Add(Anomaly(
                "expired_pending",
                "critical",
                $"{orderStats.Expired} pending offline order(s) past expiry awaiting cleanup.",
                detectedAt));
        }

        if (orderDetail.ExpiringWithin6Hours > 0)
        {
            anomalies.Add(Anomaly(
                "old_pending",
                "critical",
                $"{orderDetail.ExpiringWithin6Hours} offline order(s) expire within {opts.ExpiryCriticalHours} hours.",
                detectedAt));
        }
        else if (orderDetail.ExpiringWithin24Hours > 0)
        {
            anomalies.Add(Anomaly(
                "old_pending",
                "warning",
                $"{orderDetail.ExpiringWithin24Hours} offline order(s) expire within {opts.ExpiryWarningHours} hours.",
                detectedAt));
        }

        foreach (var row in orderDetail.ByRegister.Where(r => r.PendingCount > opts.OrderQueueAlertThreshold))
        {
            registerLabels.TryGetValue(row.CashRegisterId, out var label);
            anomalies.Add(Anomaly(
                "too_many_pending",
                row.PendingCount > opts.OrderQueueAlertThreshold * 2 ? "critical" : "warning",
                $"Cash register {label ?? row.RegisterNumber} has {row.PendingCount} pending offline orders (threshold {opts.OrderQueueAlertThreshold}).",
                detectedAt));
        }

        if (syncDetail.StalledPendingOrderCount > 0)
        {
            anomalies.Add(Anomaly(
                "sync_failure",
                "warning",
                $"{syncDetail.StalledPendingOrderCount} pending offline order(s) have not synced for over {opts.StalledSyncHours} hours.",
                detectedAt));
        }

        if (syncDetail.MaxSyncAttemptsReachedCount > 0)
        {
            anomalies.Add(Anomaly(
                "sync_failure",
                "critical",
                $"{syncDetail.MaxSyncAttemptsReachedCount} offline order(s) reached {rules.MaxSyncRetries} sync retries.",
                detectedAt));
        }

        if (syncHealth.TotalAttempts > 0 && syncHealth.SuccessRate < rules.MinSyncSuccessRate)
        {
            anomalies.Add(Anomaly(
                "sync_failure",
                "warning",
                $"Offline sync success rate {syncHealth.SuccessRate}% is below minimum {rules.MinSyncSuccessRate}%.",
                detectedAt));
        }

        foreach (var row in transactionStats.ByRegister.Where(r =>
                     r.PendingCount > opts.TransactionQueueAlertThreshold))
        {
            registerLabels.TryGetValue(row.CashRegisterId, out var label);
            anomalies.Add(Anomaly(
                "too_many_pending",
                row.PendingCount > opts.TransactionQueueAlertThreshold * 2 ? "critical" : "warning",
                $"Cash register {label ?? row.RegisterNumber} has {row.PendingCount} pending offline TSE intents (threshold {opts.TransactionQueueAlertThreshold}).",
                detectedAt));
        }

        var maxOffline = LicenseEnforcementPolicy.GetMaxOfflineTransactionsPerCashRegister(
            _environment,
            _tseOptions.CurrentValue);
        if (maxOffline < LicenseEnforcementPolicy.MaxOfflineTransactionsUnlimited)
        {
            var warnAt = (int)Math.Floor(maxOffline * (opts.TseOfflineCapWarningPercent / 100.0));
            foreach (var row in transactionStats.ByRegister)
            {
                if (row.PendingCount < warnAt)
                    continue;

                registerLabels.TryGetValue(row.CashRegisterId, out var label);
                anomalies.Add(Anomaly(
                    row.PendingCount >= maxOffline ? "tse_cap_reached" : "tse_cap_warning",
                    row.PendingCount >= maxOffline ? "critical" : "warning",
                    $"Cash register {label ?? row.RegisterNumber} has {row.PendingCount}/{maxOffline} TSE offline transactions.",
                    detectedAt));
            }
        }

        if (transactionStats.ClockDriftWarningCount > 0)
        {
            anomalies.Add(Anomaly(
                "clock_drift",
                "warning",
                $"{transactionStats.ClockDriftWarningCount} offline TSE intent(s) flagged for clock drift.",
                detectedAt));
        }

        if (transactionStats.SequenceGapCount > 0)
        {
            anomalies.Add(Anomaly(
                "sequence_gap",
                "critical",
                $"{transactionStats.SequenceGapCount} offline TSE intent(s) detected sequence gaps.",
                detectedAt));
        }

        return anomalies;
    }

    public async Task<SyncHealth> GetSyncHealthAsync(CancellationToken ct = default)
    {
        var detail = await LoadSyncHealthDetailAsync(ct).ConfigureAwait(false);

        var totalAttempts = await _context.OfflineOrders.AsNoTracking()
            .Where(o => o.SyncAttempts > 0)
            .SumAsync(o => (int?)o.SyncAttempts, ct)
            .ConfigureAwait(false) ?? 0;

        var failedAttempts = await _context.OfflineOrders.AsNoTracking()
            .CountAsync(
                o => o.Status == OfflineOrderStatuses.Failed
                     || (o.SyncAttempts >= _alertRules.CurrentValue.MaxSyncRetries
                         && o.Status == OfflineOrderStatuses.Pending),
                ct)
            .ConfigureAwait(false);

        var syncedWithAttempts = await _context.OfflineOrders.AsNoTracking()
            .CountAsync(o => o.Status == OfflineOrderStatuses.Synced && o.SyncAttempts > 0, ct)
            .ConfigureAwait(false);

        var successDenominator = syncedWithAttempts + failedAttempts;
        var successRate = successDenominator == 0
            ? 100
            : (int)Math.Round(100.0 * syncedWithAttempts / successDenominator);

        var isHealthy = detail.StalledPendingOrderCount == 0
                        && detail.MaxSyncAttemptsReachedCount == 0;

        return new SyncHealth
        {
            IsHealthy = isHealthy,
            AvgSyncTimeMs = (int)Math.Round(detail.AverageOrderSyncLatencySeconds * 1000),
            SuccessRate = successRate,
            TotalAttempts = totalAttempts,
            FailedAttempts = failedAttempts,
        };
    }

    private async Task<OrderMonitoringDetail> LoadOrderMonitoringDetailAsync(CancellationToken ct)
    {
        RequireTenantId();
        var now = DateTime.UtcNow;
        var warningCutoff = now.AddHours(_options.CurrentValue.ExpiryWarningHours);
        var criticalCutoff = now.AddHours(_options.CurrentValue.ExpiryCriticalHours);
        var ageCutoff = now.AddHours(-Math.Max(1, _alertRules.CurrentValue.MaxPendingAgeHours));

        var pendingActive = _context.OfflineOrders.AsNoTracking()
            .Where(o => o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc > now);

        return new OrderMonitoringDetail
        {
            ExpiringWithin24Hours = await pendingActive
                .CountAsync(o => o.ExpiresAtUtc <= warningCutoff, ct)
                .ConfigureAwait(false),
            ExpiringWithin6Hours = await pendingActive
                .CountAsync(o => o.ExpiresAtUtc <= criticalCutoff, ct)
                .ConfigureAwait(false),
            PendingOlderThanHours = await pendingActive
                .CountAsync(o => o.CreatedAtUtc <= ageCutoff, ct)
                .ConfigureAwait(false),
            OldestPendingCreatedAtUtc = await pendingActive
                .OrderBy(o => o.CreatedAtUtc)
                .Select(o => (DateTime?)o.CreatedAtUtc)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false),
            ByRegister = await BuildOrderRegisterBreakdownAsync(now, ct).ConfigureAwait(false),
        };
    }

    private async Task<SyncHealthDetail> LoadSyncHealthDetailAsync(CancellationToken ct)
    {
        RequireTenantId();
        var now = DateTime.UtcNow;
        var stalledCutoff = now.AddHours(-Math.Max(1, _options.CurrentValue.StalledSyncHours));

        var stalledCount = await _context.OfflineOrders.AsNoTracking()
            .CountAsync(
                o => o.Status == OfflineOrderStatuses.Pending
                     && o.ExpiresAtUtc > now
                     && (o.LastSyncAttemptUtc == null
                         ? o.CreatedAtUtc < stalledCutoff
                         : o.LastSyncAttemptUtc < stalledCutoff),
                ct)
            .ConfigureAwait(false);

        var maxAttemptsCount = await _context.OfflineOrders.AsNoTracking()
            .CountAsync(
                o => o.SyncAttempts >= _alertRules.CurrentValue.MaxSyncRetries
                     && (o.Status == OfflineOrderStatuses.Failed
                         || o.Status == OfflineOrderStatuses.Pending),
                ct)
            .ConfigureAwait(false);

        var latencyRows = await _context.OfflineOrders.AsNoTracking()
            .Where(o => o.Status == OfflineOrderStatuses.Synced
                        && o.SyncedAtUtc != null
                        && o.LastSyncAttemptUtc != null)
            .OrderByDescending(o => o.SyncedAtUtc)
            .Take(200)
            .Select(o => new { o.LastSyncAttemptUtc, o.SyncedAtUtc })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var latencySamples = latencyRows
            .Select(o => (o.SyncedAtUtc!.Value - o.LastSyncAttemptUtc!.Value).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .ToList();

        return new SyncHealthDetail
        {
            StalledPendingOrderCount = stalledCount,
            MaxSyncAttemptsReachedCount = maxAttemptsCount,
            AverageOrderSyncLatencySeconds = latencySamples.Count > 0 ? latencySamples.Average() : 0,
        };
    }

    private async Task<IReadOnlyList<OfflineRegisterQueueSummary>> BuildOrderRegisterBreakdownAsync(
        DateTime now,
        CancellationToken ct)
    {
        var rows = await (
            from o in _context.OfflineOrders.AsNoTracking()
            join cr in _context.CashRegisters.AsNoTracking() on o.CashRegisterId equals cr.Id
            where o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc > now
            group o by new { o.CashRegisterId, cr.RegisterNumber } into g
            select new OfflineRegisterQueueSummary
            {
                CashRegisterId = g.Key.CashRegisterId,
                RegisterNumber = g.Key.RegisterNumber,
                PendingCount = g.Count(),
                FailedCount = 0,
            }).ToListAsync(ct).ConfigureAwait(false);

        var failedByRegister = await _context.OfflineOrders.AsNoTracking()
            .Where(o => o.Status == OfflineOrderStatuses.Failed)
            .GroupBy(o => o.CashRegisterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            rows[i] = row with { FailedCount = failedByRegister.GetValueOrDefault(row.CashRegisterId) };
        }

        var pendingRegisterIds = rows.Select(r => r.CashRegisterId).ToHashSet();
        foreach (var (registerId, count) in failedByRegister)
        {
            if (pendingRegisterIds.Contains(registerId))
                continue;

            var regNum = await _context.CashRegisters.AsNoTracking()
                .Where(cr => cr.Id == registerId)
                .Select(cr => cr.RegisterNumber)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            rows.Add(new OfflineRegisterQueueSummary
            {
                CashRegisterId = registerId,
                RegisterNumber = regNum ?? registerId.ToString(),
                PendingCount = 0,
                FailedCount = count,
            });
        }

        return rows.OrderBy(r => r.RegisterNumber).ToList();
    }

    private async Task<IReadOnlyList<OfflineRegisterQueueSummary>> BuildTransactionRegisterBreakdownAsync(
        CancellationToken ct)
    {
        var rows = await (
            from t in _context.OfflineTransactions.AsNoTracking()
            join cr in _context.CashRegisters.AsNoTracking() on t.CashRegisterId equals cr.Id
            where t.Status == OfflineTransactionStatus.Pending
                  || t.Status == OfflineTransactionStatus.NonFiscalPending
            group t by new { t.CashRegisterId, cr.RegisterNumber } into g
            select new OfflineRegisterQueueSummary
            {
                CashRegisterId = g.Key.CashRegisterId,
                RegisterNumber = g.Key.RegisterNumber,
                PendingCount = g.Count(),
                FailedCount = 0,
            }).ToListAsync(ct).ConfigureAwait(false);

        var failedByRegister = await _context.OfflineTransactions.AsNoTracking()
            .Where(t => t.Status == OfflineTransactionStatus.Failed)
            .GroupBy(t => t.CashRegisterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);

        return rows
            .Select(row => row with { FailedCount = failedByRegister.GetValueOrDefault(row.CashRegisterId) })
            .OrderBy(r => r.RegisterNumber)
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> LoadRegisterLabelsAsync(CancellationToken ct) =>
        await _context.CashRegisters.AsNoTracking()
            .Select(cr => new { cr.Id, Label = cr.RegisterNumber + " · " + cr.Location })
            .ToDictionaryAsync(x => x.Id, x => x.Label, ct)
            .ConfigureAwait(false);

    private static OfflineAnomaly Anomaly(string code, string severity, string message, DateTime detectedAt) =>
        new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            DetectedAt = detectedAt,
        };

    private static DateTime? MaxUtc(params DateTime?[] values)
    {
        DateTime? max = null;
        foreach (var value in values)
        {
            if (!value.HasValue)
                continue;
            if (!max.HasValue || value.Value > max.Value)
                max = value;
        }

        return max;
    }

    private Guid RequireTenantId()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required for offline monitoring.");

        return tenantId;
    }

    private sealed class OrderMonitoringDetail
    {
        public int ExpiringWithin24Hours { get; init; }
        public int ExpiringWithin6Hours { get; init; }
        public int PendingOlderThanHours { get; init; }
        public DateTime? OldestPendingCreatedAtUtc { get; init; }
        public IReadOnlyList<OfflineRegisterQueueSummary> ByRegister { get; init; } =
            Array.Empty<OfflineRegisterQueueSummary>();
    }

    private sealed class SyncHealthDetail
    {
        public int StalledPendingOrderCount { get; init; }
        public int MaxSyncAttemptsReachedCount { get; init; }
        public double AverageOrderSyncLatencySeconds { get; init; }
    }
}
