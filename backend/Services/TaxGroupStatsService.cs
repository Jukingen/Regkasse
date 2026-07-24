using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Tax-group catalog distribution + period Umsatz (receipt tax lines by rate).
/// </summary>
public sealed class TaxGroupStatsService : ITaxGroupStatsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TaxGroupStatsService> _logger;

    public TaxGroupStatsService(AppDbContext db, ILogger<TaxGroupStatsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TaxGroupStatsReport> GetStatsAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var (rangeStart, rangeEnd) = NormalizeUtcRange(periodStartUtc, periodEndUtc);

        var groups = await _db.TaxGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .OrderByDescending(g => g.IsDefault)
            .ThenBy(g => g.Rate)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var productCounts = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .GroupBy(p => p.TaxGroupId)
            .Select(g => new { TaxGroupId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var countByGroupId = productCounts.ToDictionary(x => x.TaxGroupId, x => x.Count);
        var totalProducts = productCounts.Sum(x => x.Count);

        var revenueByRate = await (
                from line in _db.ReceiptTaxLines.AsNoTracking()
                join receipt in _db.Receipts.AsNoTracking() on line.ReceiptId equals receipt.ReceiptId
                where line.TenantId == tenantId
                      && receipt.TenantId == tenantId
                      && receipt.IssuedAt >= rangeStart
                      && receipt.IssuedAt < rangeEnd
                group line by line.TaxRate
                into g
                select new
                {
                    Rate = g.Key,
                    Gross = g.Sum(x => x.GrossAmount),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var revenueLookup = revenueByRate.ToDictionary(
            x => Round2(x.Rate),
            x => Round2(x.Gross));

        // One canonical owner per rate so overlapping custom groups do not double-count Umsatz.
        var revenueOwnerByRate = groups
            .Where(g => g.IsActive)
            .GroupBy(g => Round2(g.Rate))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsSystem)
                    .ThenByDescending(x => x.IsDefault)
                    .ThenBy(x => x.Name)
                    .First()
                    .Id);

        var stats = new List<TaxGroupStat>();
        foreach (var group in groups)
        {
            var productCount = countByGroupId.GetValueOrDefault(group.Id);
            if (!group.IsActive && productCount == 0)
                continue;

            var rate = Round2(group.Rate);
            var ownsRevenue = revenueOwnerByRate.TryGetValue(rate, out var ownerId) && ownerId == group.Id;
            var revenue = ownsRevenue && revenueLookup.TryGetValue(rate, out var gross) ? gross : 0m;
            var percentage = totalProducts > 0
                ? Round2(100m * productCount / totalProducts)
                : 0m;

            stats.Add(new TaxGroupStat
            {
                Id = group.Id,
                Name = group.Name,
                Rate = rate,
                Color = group.Color,
                Icon = group.Icon,
                IsActive = group.IsActive,
                ProductCount = productCount,
                Revenue = revenue,
                Percentage = percentage,
            });
        }

        var report = new TaxGroupStatsReport
        {
            PeriodStart = rangeStart,
            PeriodEnd = rangeEnd,
            TotalProducts = totalProducts,
            TotalRevenue = Round2(revenueLookup.Values.Sum()),
            Groups = stats,
        };

        _logger.LogInformation(
            "Tax group stats for tenant {TenantId}: {GroupCount} groups, products={Products}, revenue={Revenue}",
            tenantId,
            stats.Count,
            totalProducts,
            report.TotalRevenue);

        return report;
    }

    private static (DateTime Start, DateTime EndExclusive) NormalizeUtcRange(DateTime start, DateTime end)
    {
        var rangeStart = start.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(start, DateTimeKind.Utc)
            : start.ToUniversalTime();
        var rangeEnd = end.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(end, DateTimeKind.Utc)
            : end.ToUniversalTime();

        if (rangeEnd <= rangeStart)
            throw new ArgumentException("Period end must be after period start.");

        var maxDays = 366;
        if ((rangeEnd - rangeStart).TotalDays > maxDays)
            throw new ArgumentException($"Period must not exceed {maxDays} days.");

        return (rangeStart, rangeEnd);
    }

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
