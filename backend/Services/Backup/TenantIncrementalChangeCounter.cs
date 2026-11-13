using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Counts tenant rows changed since a UTC watermark (preview for incremental packages).</summary>
internal static class TenantIncrementalChangeCounter
{
    public static async Task<IReadOnlyDictionary<string, int>> CountAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime sinceUtc,
        CancellationToken ct)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        counts["products.json"] = await CountBaseAsync(
            db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), sinceUtc, ct);
        counts["categories.json"] = await CountBaseAsync(
            db.Categories.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), sinceUtc, ct);
        counts["customers.json"] = await CountBaseAsync(
            db.Customers.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), sinceUtc, ct);

        var cashRegisterIds = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        counts["payment_details.json"] = await CountBaseAsync(
            db.PaymentDetails.AsNoTracking().Where(p => cashRegisterIds.Contains(p.CashRegisterId)), sinceUtc, ct);
        counts["receipts.json"] = await db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && (x.CreatedAt >= sinceUtc || x.IssuedAt >= sinceUtc))
            .CountAsync(ct);
        counts["daily_closings.json"] = await db.DailyClosings.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId
                        && (x.CreatedAt >= sinceUtc || (x.UpdatedAt != null && x.UpdatedAt >= sinceUtc)))
            .CountAsync(ct);
        counts["vouchers.json"] = await db.Vouchers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.CreatedAtUtc >= sinceUtc)
            .CountAsync(ct);
        counts["offline_orders.json"] = await db.OfflineOrders.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.CreatedAtUtc >= sinceUtc)
            .CountAsync(ct);
        counts["audit_logs.json"] = await db.AuditLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Timestamp >= sinceUtc)
            .CountAsync(ct);
        counts["activity_events.json"] = await db.ActivityEvents.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.CreatedAtUtc >= sinceUtc)
            .CountAsync(ct);

        return counts;
    }

    private static Task<int> CountBaseAsync<T>(IQueryable<T> query, DateTime sinceUtc, CancellationToken ct)
        where T : BaseEntity =>
        query.Where(x => x.CreatedAt >= sinceUtc || (x.UpdatedAt != null && x.UpdatedAt >= sinceUtc))
            .CountAsync(ct);
}
