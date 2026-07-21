using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Metrics;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Periodically refreshes business gauges (tenants, revenue, active orders, users) from the database.
/// </summary>
public sealed class BusinessMetricsRefreshHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly string[] ActiveOrderStatuses =
    [
        OnlineOrderStatuses.Pending,
        OnlineOrderStatuses.Accepted,
        OnlineOrderStatuses.Preparing,
        OnlineOrderStatuses.Ready,
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBusinessMetricsService _metrics;
    private readonly ILogger<BusinessMetricsRefreshHostedService> _logger;

    public BusinessMetricsRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        IBusinessMetricsService metrics,
        ILogger<BusinessMetricsRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial refresh shortly after startup (skip OpenAPI export / one-shot tooling).
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Business metrics refresh failed");
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

    private async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenants = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(t => t.IsActive && t.DeletedAtUtc == null, ct)
            .ConfigureAwait(false);

        var users = await db.Users.AsNoTracking()
            .CountAsync(ct)
            .ConfigureAwait(false);

        var activeOrders = await db.OnlineOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(o => ActiveOrderStatuses.Contains(o.OrderStatus), ct)
            .ConfigureAwait(false);

        // Lifetime POS payment gross total (EUR). Non-fiscal online orders are excluded here.
        var revenue = await db.PaymentDetails.AsNoTracking()
            .IgnoreQueryFilters()
            .SumAsync(p => (decimal?)p.TotalAmount, ct)
            .ConfigureAwait(false) ?? 0m;

        _metrics.UpdateTenantCount(tenants);
        _metrics.UpdateRegisteredUsers(users);
        _metrics.UpdateActiveOrders(activeOrders);
        _metrics.UpdateRevenue(revenue);

        _logger.LogDebug(
            "Business metrics refreshed: tenants={Tenants}, users={Users}, activeOrders={ActiveOrders}, revenueEur={Revenue}",
            tenants,
            users,
            activeOrders,
            revenue);
    }
}
