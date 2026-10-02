using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>POS offline sync health — tenant-scoped pending queue snapshot.</summary>
[ApiController]
[Route("api/pos/offline")]
public class OfflineHealthController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<OfflineAlertRules> _alertRules;
    private readonly ITenantLimitGuard _limitGuard;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public OfflineHealthController(
        AppDbContext db,
        IOptionsMonitor<OfflineAlertRules> alertRules,
        ITenantLimitGuard limitGuard,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<OfflineHealthController> logger) : base(logger)
    {
        _db = db;
        _alertRules = alertRules;
        _limitGuard = limitGuard;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("health")]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> GetSyncHealth(CancellationToken cancellationToken)
    {
        var maxPending = Math.Max(1, _alertRules.CurrentValue.MaxPendingOrders);
        var warningThreshold = (int)Math.Ceiling(maxPending * 0.8);

        var pendingCount = await _db.OfflineOrders
            .AsNoTracking()
            .CountAsync(o => o.Status == OfflineOrderStatuses.Pending, cancellationToken);

        var lastSyncAt = await _db.OfflineOrders
            .AsNoTracking()
            .Where(o => o.SyncedAtUtc != null)
            .OrderByDescending(o => o.SyncedAtUtc)
            .Select(o => o.SyncedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var isHealthy = pendingCount < warningThreshold;
        var currentOffline = 0;
        var maxOffline = TenantLimits.DefaultMaxOfflineTransactions;
        if (_tenantAccessor.TenantId is Guid tenantId && tenantId != Guid.Empty)
        {
            try
            {
                var usage = await _limitGuard.GetUsageAsync(tenantId, cancellationToken).ConfigureAwait(false);
                currentOffline = usage.CurrentOfflineTransactions;
                maxOffline = Math.Max(1, usage.Limits.MaxOfflineTransactions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "POS offline health: tenant limit usage unavailable");
            }
        }

        var syncHealth = new PosOfflineSyncHealthDto
        {
            PendingOrders = pendingCount,
            MaxPending = maxPending,
            IsHealthy = isHealthy,
            Status = isHealthy ? "healthy" : "warning",
            LastSyncAt = lastSyncAt,
            CurrentOfflineTransactions = currentOffline,
            MaxOfflineTransactions = maxOffline,
        };

        return Ok(new { success = true, data = syncHealth, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Display snapshot for POS TSE-offline capacity. Does not change HTTP 409 enforcement.
    /// Count is pending <c>offline_transactions</c> plus pending <c>offline_orders</c>.
    /// </summary>
    [HttpGet("~/api/pos/offline-limit")]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> GetOfflineLimit(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return NotFound();
        }

        var maxOffline = TenantLimits.DefaultMaxOfflineTransactions;
        var pendingIntents = 0;
        try
        {
            var usage = await _limitGuard.GetUsageAsync(tenantId, cancellationToken).ConfigureAwait(false);
            maxOffline = Math.Max(1, usage.Limits.MaxOfflineTransactions);
            pendingIntents = Math.Max(0, usage.CurrentOfflineTransactions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "POS offline-limit: tenant limit usage unavailable");
            pendingIntents = await _db.OfflineTransactions
                .AsNoTracking()
                .CountAsync(
                    o => o.Status == OfflineTransactionStatus.Pending
                         || o.Status == OfflineTransactionStatus.NonFiscalPending,
                    cancellationToken);
        }

        var pendingOrders = await _db.OfflineOrders
            .AsNoTracking()
            .CountAsync(o => o.Status == OfflineOrderStatuses.Pending, cancellationToken);

        var current = pendingIntents + pendingOrders;
        var dto = new PosOfflineLimitDto
        {
            MaxOfflineTransactions = maxOffline,
            CurrentOfflineTransactions = current,
            ApproachingLimit = maxOffline > 0 && current * 100 >= maxOffline * 80,
            LimitReached = maxOffline > 0 && current >= maxOffline,
        };

        return Ok(new { success = true, data = dto, timestamp = DateTime.UtcNow });
    }
}
