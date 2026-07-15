using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
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

    public OfflineHealthController(
        AppDbContext db,
        IOptionsMonitor<OfflineAlertRules> alertRules,
        ILogger<OfflineHealthController> logger) : base(logger)
    {
        _db = db;
        _alertRules = alertRules;
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
        var syncHealth = new PosOfflineSyncHealthDto
        {
            PendingOrders = pendingCount,
            MaxPending = maxPending,
            IsHealthy = isHealthy,
            Status = isHealthy ? "healthy" : "warning",
            LastSyncAt = lastSyncAt,
        };

        return Ok(new { success = true, data = syncHealth, timestamp = DateTime.UtcNow });
    }
}
