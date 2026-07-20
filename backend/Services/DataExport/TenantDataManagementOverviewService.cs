using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataRetention;
using KasseAPI_Final.Services.License;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DataExport;

public interface ITenantDataManagementOverviewService
{
    Task<TenantDataManagementOverviewDto> ListAsync(CancellationToken ct = default);
}

/// <summary>Cross-tenant data-management dashboard for Super Admin (RKSV-aware).</summary>
public sealed class TenantDataManagementOverviewService : ITenantDataManagementOverviewService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILicenseLifecycleResolver _lifecycle;

    public TenantDataManagementOverviewService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILicenseLifecycleResolver lifecycle)
    {
        _dbFactory = dbFactory;
        _lifecycle = lifecycle;
    }

    public async Task<TenantDataManagementOverviewDto> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var tenants = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.Name,
                t.LicenseValidUntilUtc,
                t.CustomerDataPurgedAtUtc,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var pendingStatuses = new[]
        {
            TenantDataDeletionRequestStatuses.Pending,
            TenantDataDeletionRequestStatuses.ExportReady,
            TenantDataDeletionRequestStatuses.Confirmed,
        };

        var deletionRows = await db.TenantDataDeletionRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => pendingStatuses.Contains(r.Status))
            .Select(r => new { r.TenantId, r.Status, r.RequestedAtUtc })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var deletionByTenant = deletionRows
            .GroupBy(r => r.TenantId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.OrderByDescending(x => x.RequestedAtUtc).First();
                    return new { latest.Status, latest.RequestedAtUtc };
                });

        var paymentStats = await (
                from p in db.PaymentDetails.AsNoTracking()
                join cr in db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                    on p.CashRegisterId equals cr.Id
                group p by cr.TenantId
                into g
                select new
                {
                    TenantId = g.Key,
                    Count = g.Count(),
                    Oldest = g.Min(x => x.CreatedAt),
                })
            .ToDictionaryAsync(x => x.TenantId, ct)
            .ConfigureAwait(false);

        var items = new List<TenantDataManagementOverviewItemDto>(tenants.Count);
        var inGrace = 0;
        var locked = 0;
        var pendingDeletion = 0;
        var purged = 0;

        foreach (var t in tenants)
        {
            var hasPending = deletionByTenant.ContainsKey(t.Id);
            var lifecycle = _lifecycle.Resolve(t.LicenseValidUntilUtc, t.CustomerDataPurgedAtUtc, hasPending, now);

            var daysOverdue = 0;
            if (t.LicenseValidUntilUtc.HasValue)
            {
                var until = DateTime.SpecifyKind(t.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
                daysOverdue = Math.Max(0, (now - until).Days);
            }

            var isGrace = lifecycle == LicenseLifecycleState.Grace;
            var isLocked = lifecycle is LicenseLifecycleState.Locked or LicenseLifecycleState.Archived;
            var isArchived = lifecycle == LicenseLifecycleState.Archived;
            var graceRemaining = isGrace
                ? Math.Max(0, LicenseGracePeriodConfig.GracePeriodDays - daysOverdue)
                : 0;

            if (isGrace) inGrace++;
            if (isLocked) locked++;
            if (hasPending) pendingDeletion++;
            if (t.CustomerDataPurgedAtUtc.HasValue) purged++;

            paymentStats.TryGetValue(t.Id, out var pay);
            deletionByTenant.TryGetValue(t.Id, out var del);

            DateTime? retentionUntil = pay?.Oldest.AddYears(RksvDataRetentionService.RetentionYears);

            items.Add(new TenantDataManagementOverviewItemDto
            {
                TenantId = t.Id,
                TenantSlug = t.Slug,
                TenantName = t.Name,
                LifecycleState = lifecycle.ToString(),
                LicenseValidUntilUtc = t.LicenseValidUntilUtc,
                DaysOverdue = daysOverdue,
                IsInGracePeriod = isGrace,
                GracePeriodRemainingDays = graceRemaining,
                IsLocked = isLocked,
                IsArchived = isArchived,
                CustomerDataPurgedAtUtc = t.CustomerDataPurgedAtUtc,
                HasPendingDeletionRequest = hasPending,
                DeletionRequestStatus = del?.Status,
                DeletionRequestedAtUtc = del?.RequestedAtUtc,
                OldestRksvPaymentDate = pay?.Oldest,
                RksvRetentionUntil = retentionUntil,
                RksvPaymentCount = pay?.Count ?? 0,
            });
        }

        return new TenantDataManagementOverviewDto
        {
            Items = items,
            TotalTenants = items.Count,
            InGraceCount = inGrace,
            LockedCount = locked,
            PendingDeletionRequestCount = pendingDeletion,
            PurgedCount = purged,
        };
    }
}
