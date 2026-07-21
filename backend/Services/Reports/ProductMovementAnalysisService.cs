using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

public interface IProductMovementAnalysisService
{
    Task<ProductMovementReportDto> GetProductMovementAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
}

public sealed class ProductMovementAnalysisService : IProductMovementAnalysisService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;

    public ProductMovementAnalysisService(AppDbContext db, ISettingsTenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<ProductMovementReportDto> GetProductMovementAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var (fromUtc, endBoundUtc, endExclusive, repStart, repEnd) = AdminReportQueryRange.Resolve(startDate, endDate);
        var periodDays = Math.Max((repEnd - repStart).TotalDays + 1, 1);

        var orderItems = endExclusive
            ? await _db.OrderItems.AsNoTracking()
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.OrderDate >= fromUtc && oi.Order.OrderDate < endBoundUtc && oi.Order.IsActive)
                .Where(oi => _db.Products.Any(pr => pr.Id == oi.ProductId && pr.TenantId == tenantId))
                .ToListAsync(cancellationToken)
            : await _db.OrderItems.AsNoTracking()
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.OrderDate >= fromUtc && oi.Order.OrderDate <= endBoundUtc && oi.Order.IsActive)
                .Where(oi => _db.Products.Any(pr => pr.Id == oi.ProductId && pr.TenantId == tenantId))
                .ToListAsync(cancellationToken);

        var grouped = orderItems
            .GroupBy(oi => new { oi.ProductId, oi.ProductName })
            .Select(g => new ProductMovementItemDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalAmount),
                VelocityPerDay = g.Sum(x => x.Quantity) / periodDays,
            })
            .ToList();

        var topQty = grouped.OrderByDescending(x => x.QuantitySold).Take(15).ToList();
        var topRev = grouped.OrderByDescending(x => x.Revenue).Take(15).ToList();
        var slow = grouped
            .Where(x => x.QuantitySold > 0)
            .OrderBy(x => x.VelocityPerDay)
            .Take(15)
            .ToList();

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .Select(p => new { p.Id, p.Price, p.StockQuantity })
            .ToListAsync(cancellationToken);

        var inventory = await _db.Inventory.AsNoTracking()
            .Where(i => _db.Products.Any(p => p.Id == i.ProductId && p.TenantId == tenantId))
            .Select(i => new { i.ProductId, i.UnitCost, i.CurrentStock })
            .ToListAsync(cancellationToken);

        var cogs = orderItems.Sum(oi =>
        {
            var inv = inventory.FirstOrDefault(x => x.ProductId == oi.ProductId);
            var unitCost = inv?.UnitCost ?? 0m;
            return unitCost * oi.Quantity;
        });

        if (cogs <= 0)
            cogs = orderItems.Sum(oi => oi.TotalAmount) * 0.5m;

        var avgInventoryValue = products.Sum(p =>
        {
            var inv = inventory.FirstOrDefault(i => i.ProductId == p.Id);
            var unit = inv?.UnitCost > 0 ? inv.UnitCost : p.Price;
            var qty = inv?.CurrentStock ?? p.StockQuantity;
            return unit * qty;
        });

        var turnover = avgInventoryValue > 0 ? cogs / avgInventoryValue : 0m;
        var dailyCogs = cogs / (decimal)periodDays;
        var daysOnHand = dailyCogs > 0 ? (double)(avgInventoryValue / dailyCogs) : 0;

        var topForSeason = topRev.Take(5).Select(x => x.ProductId).ToHashSet();
        var seasonal = orderItems
            .Where(oi => topForSeason.Contains(oi.ProductId))
            .GroupBy(oi => new { oi.ProductId, oi.ProductName, Month = oi.Order.OrderDate.ToString("yyyy-MM") })
            .GroupBy(x => new { x.Key.ProductId, x.Key.ProductName })
            .Select(g => new ProductSeasonalTrendDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                MonthlySales = g
                    .OrderBy(m => m.Key.Month)
                    .Select(m => new ProductMonthlySalesDto { Month = m.Key.Month, Quantity = m.Sum(x => x.Quantity) })
                    .ToList(),
            })
            .ToList();

        var legacyLines = grouped
            .OrderByDescending(l => l.Revenue)
            .Take(100)
            .Select(x => new ProductMovementLineDto
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                QuantitySold = x.QuantitySold,
                Revenue = x.Revenue,
            })
            .ToList();

        var stockMovements = await (
            from tx in _db.InventoryTransactions.AsNoTracking()
            join inv in _db.Inventory.AsNoTracking() on tx.InventoryId equals inv.Id
            join pr in _db.Products.AsNoTracking() on inv.ProductId equals pr.Id
            where pr.TenantId == tenantId
                  && (endExclusive
                      ? tx.TransactionDate >= fromUtc && tx.TransactionDate < endBoundUtc
                      : tx.TransactionDate >= fromUtc && tx.TransactionDate <= endBoundUtc)
            orderby tx.TransactionDate descending
            select new InventoryMovementLineDto
            {
                ProductId = pr.Id,
                ProductName = pr.Name,
                TransactionType = tx.TransactionType.ToString(),
                Quantity = tx.Quantity,
                TransactionDateUtc = tx.TransactionDate,
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return new ProductMovementReportDto
        {
            PeriodStartLocal = repStart,
            PeriodEndLocal = repEnd,
            Meta = new OperationalReportMetaDto
            {
                SchemaVersion = "2.0-product-movement",
                ReportGeneratedAtUtc = DateTime.UtcNow,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = endBoundUtc,
                PeriodStartLocalDate = repStart,
                PeriodEndLocalDate = repEnd,
            },
            TopSellingByQuantity = topQty,
            TopSellingByRevenue = topRev,
            SlowMovers = slow,
            StockTurnoverRate = turnover,
            DaysOfInventoryOnHand = daysOnHand,
            SeasonalTrends = seasonal,
            Lines = legacyLines,
            StockMovements = stockMovements,
        };
    }
}
