using System.Diagnostics;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin system metrics for the FA dashboard (JSON summary; Prometheus remains at <c>/metrics</c>).</summary>
[ApiController]
[Route("api/admin/metrics")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private static readonly DateTime ProcessStartedUtc =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private static readonly string[] ActiveOrderStatuses =
    [
        OnlineOrderStatuses.Pending,
        OnlineOrderStatuses.Accepted,
        OnlineOrderStatuses.Preparing,
        OnlineOrderStatuses.Ready,
    ];

    private readonly IBusinessMetricsService _businessMetrics;
    private readonly ICacheMetricsService _cacheMetrics;
    private readonly ApiRequestMetricsAccumulator _apiRequests;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHostEnvironment _environment;

    public MetricsController(
        IBusinessMetricsService businessMetrics,
        ICacheMetricsService cacheMetrics,
        ApiRequestMetricsAccumulator apiRequests,
        IDbContextFactory<AppDbContext> dbFactory,
        IHostEnvironment environment)
    {
        _businessMetrics = businessMetrics;
        _cacheMetrics = cacheMetrics;
        _apiRequests = apiRequests;
        _dbFactory = dbFactory;
        _environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SystemMetricsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var (totalRequests, _, avgMs, errorRate) = _apiRequests.Snapshot();
        var metrics = new SystemMetricsSummaryDto
        {
            TotalRequests = totalRequests,
            AvgResponseTime = Math.Round(avgMs, 1),
            ErrorRate = Math.Round(errorRate, 2),
            ActiveUsers = await GetActiveUsersAsync(ct).ConfigureAwait(false),
            ActiveOrders = await GetActiveOrdersAsync(ct).ConfigureAwait(false),
            ActiveTenants = await GetActiveTenantsAsync(ct).ConfigureAwait(false),
            CacheHitRatio = Math.Round(_cacheMetrics.GetHitRatio() * 100, 1),
            Uptime = GetUptime(),
            Environment = _environment.EnvironmentName,
        };

        // Keep Prometheus business gauges aligned with live DB counts for scrapers.
        _businessMetrics.UpdateRegisteredUsers(metrics.ActiveUsers);
        _businessMetrics.UpdateActiveOrders(metrics.ActiveOrders);
        _businessMetrics.UpdateTenantCount(metrics.ActiveTenants);

        return Ok(metrics);
    }

    private static long GetUptime() =>
        Math.Max(0, (long)(DateTime.UtcNow - ProcessStartedUtc).TotalSeconds);

    private async Task<int> GetActiveUsersAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Users.AsNoTracking()
            .CountAsync(u => u.IsActive, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> GetActiveOrdersAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.OnlineOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(o => ActiveOrderStatuses.Contains(o.OrderStatus), ct)
            .ConfigureAwait(false);
    }

    private async Task<int> GetActiveTenantsAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(
                t => t.DeletedAtUtc == null
                     && t.IsActive
                     && t.Status == TenantStatuses.Active,
                ct)
            .ConfigureAwait(false);
    }
}
