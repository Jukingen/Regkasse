using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Metrics;

public interface IPlatformBusinessMetricsService
{
    Task<PlatformBusinessMetricsDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Deployment-wide counts. Uses <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters"/>
/// because this is a Super Admin (<c>system.critical</c>) snapshot, not a mandant view.
/// </summary>
public sealed class PlatformBusinessMetricsService : IPlatformBusinessMetricsService
{
    internal static readonly string[] FailedFinanzOnlineStatuses =
    [
        FinanzOnlineOutboxStatuses.RetryableFailure,
        FinanzOnlineOutboxStatuses.ProtocolFailure,
        FinanzOnlineOutboxStatuses.PermanentFailure,
        FinanzOnlineOutboxStatuses.DeadLetter,
        FinanzOnlineOutboxStatuses.ManualReviewRequired,
    ];

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _time;

    public PlatformBusinessMetricsService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
    {
        _dbFactory = dbFactory;
        _time = time;
    }

    public async Task<PlatformBusinessMetricsDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var since24Hours = now.AddHours(-24);
        var since7Days = now.AddDays(-7);
        var since30Days = now.AddDays(-30);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var totalTenants = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(
                t => t.Id != SystemTenantIds.Platform && t.DeletedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);

        var receipts = await db.Receipts.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.IssuedAt >= since30Days && r.TenantId != SystemTenantIds.Platform)
            .Select(r => new ReceiptSlice(r.IssuedAt, r.TenantId, r.GrandTotal))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activePosSessions = await db.AuthSessions.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(
                s => s.RevokedAtUtc == null && s.ClientApp == ClientAppPolicy.Pos,
                cancellationToken)
            .ConfigureAwait(false);

        var failedSubmissions = await db.FinanzOnlineOutboxMessages.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(m => FailedFinanzOnlineStatuses.Contains(m.Status), cancellationToken)
            .ConfigureAwait(false);

        return new PlatformBusinessMetricsDto
        {
            GeneratedAtUtc = now,
            TotalTenants = totalTenants,
            ActiveTenantsLast30Days = receipts.Select(r => r.TenantId).Distinct().Count(),
            ReceiptsIssued = new CountWindowDto
            {
                Last24Hours = receipts.Count(r => r.IssuedAt >= since24Hours),
                Last7Days = receipts.Count(r => r.IssuedAt >= since7Days),
                Last30Days = receipts.Count,
            },
            RevenueEur = new MoneyWindowDto
            {
                Last24Hours = receipts.Where(r => r.IssuedAt >= since24Hours).Sum(r => r.GrandTotal),
                Last7Days = receipts.Where(r => r.IssuedAt >= since7Days).Sum(r => r.GrandTotal),
                Last30Days = receipts.Sum(r => r.GrandTotal),
            },
            ActivePosSessions = activePosSessions,
            FailedFinanzOnlineSubmissions = failedSubmissions,
        };
    }

    private readonly record struct ReceiptSlice(DateTime IssuedAt, Guid TenantId, decimal GrandTotal);
}
