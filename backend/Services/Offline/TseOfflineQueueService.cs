using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Offline;

public sealed class TseOfflineQueueService : ITseOfflineQueueService
{
    public const string SoftClearConfirmToken = "SOFT_CLEAR";

    private readonly AppDbContext _db;
    private readonly IActivityEventPublisher _activity;
    private readonly IAuditLogService _auditLog;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IOptionsMonitor<OfflineMonitoringOptions> _monitoringOptions;
    private readonly ILogger<TseOfflineQueueService> _logger;

    public TseOfflineQueueService(
        AppDbContext db,
        IActivityEventPublisher activity,
        IAuditLogService auditLog,
        IOptionsMonitor<TseOptions> tseOptions,
        IOptionsMonitor<OfflineMonitoringOptions> monitoringOptions,
        ILogger<TseOfflineQueueService> logger)
    {
        _db = db;
        _activity = activity;
        _auditLog = auditLog;
        _tseOptions = tseOptions;
        _monitoringOptions = monitoringOptions;
        _logger = logger;
    }

    public async Task<TseOfflineQueueStatusDto> GetQueueStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var maxPerRegister = Math.Max(1, _tseOptions.CurrentValue.MaxOfflineTransactionsPerCashRegister);
        var warning = Math.Clamp(
            _monitoringOptions.CurrentValue.TseOfflineQueueWarningThreshold,
            1,
            maxPerRegister);
        var critical = Math.Clamp(
            _monitoringOptions.CurrentValue.TseOfflineQueueCriticalThreshold,
            warning,
            Math.Max(warning, maxPerRegister));

        var queued = await QueuedQuery(tenantId)
            .AsNoTracking()
            .Select(x => new { x.Id, x.CashRegisterId, x.ServerReceivedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = queued.Count;
        var oldest = queued.Count == 0 ? (DateTime?)null : queued.Min(x => x.ServerReceivedAtUtc);
        var newest = queued.Count == 0 ? (DateTime?)null : queued.Max(x => x.ServerReceivedAtUtc);

        var registerIds = queued.Select(x => x.CashRegisterId).Distinct().ToList();
        var registerLabels = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber })
            .ToDictionaryAsync(r => r.Id, cancellationToken)
            .ConfigureAwait(false);

        var byRegister = queued
            .GroupBy(x => x.CashRegisterId)
            .Select(g =>
            {
                var count = g.Count();
                registerLabels.TryGetValue(g.Key, out var reg);
                return new TseOfflineQueueRegisterSummaryDto
                {
                    CashRegisterId = g.Key,
                    RegisterNumber = reg?.RegisterNumber,
                    QueuedCount = count,
                    MaxPerRegister = maxPerRegister,
                    IsAtCap = count >= maxPerRegister,
                    IsNearCap = count >= (int)Math.Floor(maxPerRegister * (_monitoringOptions.CurrentValue.TseOfflineCapWarningPercent / 100.0)),
                };
            })
            .OrderByDescending(r => r.QueuedCount)
            .ToList();

        return new TseOfflineQueueStatusDto
        {
            TenantId = tenantId,
            TotalQueued = total,
            CriticalThreshold = critical,
            WarningThreshold = warning,
            MaxPerRegister = maxPerRegister,
            IsCritical = total >= critical || byRegister.Any(r => r.IsAtCap),
            IsWarning = total >= warning || byRegister.Any(r => r.IsNearCap),
            OldestTransaction = oldest,
            NewestTransaction = newest,
            ByRegister = byRegister,
        };
    }

    public async Task<IReadOnlyList<TseOfflineQueuedTransactionDto>> GetQueuedTransactionsAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        limit = Math.Clamp(limit, 1, 200);

        var rows = await QueuedQuery(tenantId)
            .AsNoTracking()
            .OrderBy(x => x.ServerReceivedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerIds = rows.Select(r => r.CashRegisterId).Distinct().ToList();
        var registers = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToDictionaryAsync(r => r.Id, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(row =>
        {
            registers.TryGetValue(row.CashRegisterId, out var reg);
            var (amount, method) = TryReadPaymentSummary(row.PayloadJson);
            return new TseOfflineQueuedTransactionDto
            {
                Id = row.Id,
                CashRegisterId = row.CashRegisterId,
                CashRegisterLabel = reg is null
                    ? null
                    : $"{reg.RegisterNumber} · {reg.Location}",
                Status = row.Status.ToString(),
                ServerReceivedAtUtc = row.ServerReceivedAtUtc,
                OfflineCreatedAtUtc = row.OfflineCreatedAtUtc,
                Amount = amount,
                PaymentMethod = method,
                RetryCount = row.RetryCount,
                LastError = row.LastErrorMessageSafe,
                DeviceId = row.DeviceId,
            };
        }).ToList();
    }

    public async Task<TseOfflineQueueClearResultDto> SoftClearQueueAsync(
        Guid tenantId,
        string confirmToken,
        string? reason,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmToken, SoftClearConfirmToken, StringComparison.Ordinal))
        {
            return new TseOfflineQueueClearResultDto
            {
                Success = false,
                Error = $"confirmToken must be '{SoftClearConfirmToken}'. Hard delete is not supported.",
            };
        }

        if (tenantId == Guid.Empty)
        {
            return new TseOfflineQueueClearResultDto
            {
                Success = false,
                Error = "tenantId is required.",
            };
        }

        var rows = await QueuedQuery(tenantId)
            .Where(x => x.Status == OfflineTransactionStatus.NonFiscalPending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return new TseOfflineQueueClearResultDto
            {
                Success = true,
                SoftClearedCount = 0,
                Detail = "No NonFiscalPending intents to soft-clear.",
            };
        }

        var safeReason = string.IsNullOrWhiteSpace(reason)
            ? "Soft-cleared by SuperAdmin (TSE offline queue)."
            : reason.Trim();
        if (safeReason.Length > 500)
            safeReason = safeReason[..500];

        foreach (var row in rows)
        {
            row.Status = OfflineTransactionStatus.Failed;
            row.LastErrorCode = "SOFT_CLEARED";
            row.LastErrorMessageSafe = safeReason;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _auditLog.LogSystemOperationAsync(
                "TSE_OFFLINE_QUEUE_SOFT_CLEARED",
                "OfflineTransaction",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim(),
                userRole: "SuperAdmin",
                description: $"Soft-cleared {rows.Count} NonFiscalPending TSE offline intents.",
                status: AuditLogStatus.Success,
                responseData: new { Count = rows.Count, Reason = safeReason },
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit failed for TSE offline queue soft-clear tenant {TenantId}", tenantId);
        }

        _logger.LogWarning(
            "TSE offline queue soft-cleared for tenant {TenantId}: {Count} NonFiscalPending marked Failed by {Actor}",
            tenantId,
            rows.Count,
            actorUserId);

        return new TseOfflineQueueClearResultDto
        {
            Success = true,
            SoftClearedCount = rows.Count,
            Detail =
                $"Marked {rows.Count} NonFiscalPending intent(s) as Failed. Synced/fiscal rows were not touched.",
        };
    }

    public async Task<TseOfflineQueueAlertResultDto> SendQueueAlertAsync(
        Guid tenantId,
        int? queueSize = null,
        CancellationToken cancellationToken = default)
    {
        var status = await GetQueueStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var size = queueSize ?? status.TotalQueued;

        if (!status.IsWarning && !status.IsCritical && size < status.WarningThreshold)
        {
            return new TseOfflineQueueAlertResultDto
            {
                Sent = false,
                Severity = "Info",
                Message = $"Queue size {size} is below warning threshold ({status.WarningThreshold}).",
            };
        }

        var severity = status.IsCritical ? "Critical" : "Warning";
        var message = status.IsCritical
            ? $"TSE offline queue critically full: {size} NonFiscalPending intent(s) (threshold {status.CriticalThreshold})."
            : $"TSE offline queue filling up: {size} NonFiscalPending intent(s) (threshold {status.WarningThreshold}).";

        await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.OfflineQueueGrowing,
                new
                {
                    TotalQueued = size,
                    status.WarningThreshold,
                    status.CriticalThreshold,
                    status.IsCritical,
                    status.IsWarning,
                    OldestTransaction = status.OldestTransaction,
                    Message = message,
                },
                actorUserId: "system",
                dedupKey: $"tse-offline-queue:{tenantId:N}:{severity}:{DateTime.UtcNow:yyyyMMddHH}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new TseOfflineQueueAlertResultDto
        {
            Sent = true,
            Severity = severity,
            Message = message,
        };
    }

    private IQueryable<OfflineTransaction> QueuedQuery(Guid tenantId) =>
        _db.OfflineTransactions
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId
                        && o.Status == OfflineTransactionStatus.NonFiscalPending);

    private static (decimal Amount, string PaymentMethod) TryReadPaymentSummary(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            decimal total = 0;
            if (root.TryGetProperty("totalAmount", out var ta) && ta.TryGetDecimal(out var d))
                total = d;

            var method = "unknown";
            if (root.TryGetProperty("payment", out var pay) && pay.TryGetProperty("method", out var mEl))
            {
                var ms = mEl.GetString();
                if (!string.IsNullOrEmpty(ms))
                    method = ms.Trim().ToLowerInvariant();
            }

            return (total, method);
        }
        catch
        {
            return (0, "unknown");
        }
    }
}
