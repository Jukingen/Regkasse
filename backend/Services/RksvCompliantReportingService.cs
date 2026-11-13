using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds RKSV-oriented historical reports from receipt tax lines (sale-time rates)
/// and product price history journals.
/// </summary>
public sealed class RksvCompliantReportingService : IRksvReportingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RksvCompliantReportingService> _logger;

    public RksvCompliantReportingService(
        AppDbContext db,
        ILogger<RksvCompliantReportingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RksvReport> GenerateHistoricalReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(tenantId, fromUtc, toUtc);
        var (rangeStart, rangeEnd) = NormalizeUtcRange(fromUtc, toUtc);

        // Historical rates: receipt_tax_lines written at payment time — never products.tax_rate.
        var rows = await (
                from line in _db.ReceiptTaxLines.AsNoTracking()
                join receipt in _db.Receipts.AsNoTracking() on line.ReceiptId equals receipt.ReceiptId
                join payment in _db.PaymentDetails.AsNoTracking() on receipt.PaymentId equals payment.Id into payments
                from payment in payments.DefaultIfEmpty()
                where line.TenantId == tenantId
                      && receipt.TenantId == tenantId
                      && receipt.IssuedAt >= rangeStart
                      && receipt.IssuedAt < rangeEnd
                select new
                {
                    receipt.ReceiptId,
                    receipt.ReceiptNumber,
                    receipt.IssuedAt,
                    receipt.SubTotal,
                    receipt.TaxTotal,
                    receipt.GrandTotal,
                    receipt.SignatureValue,
                    PaymentTse = payment != null ? payment.TseSignature : null,
                    line.TaxRate,
                    line.NetAmount,
                    line.TaxAmount,
                    line.GrossAmount,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nameByRate = await LoadTaxGroupNamesAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var transactions = rows
            .Select(r =>
            {
                var rate = Round2(r.TaxRate);
                var tse = FirstNonEmpty(r.PaymentTse, r.SignatureValue);
                return new RksvTransaction
                {
                    ReceiptId = r.ReceiptId,
                    ReceiptNumber = r.ReceiptNumber,
                    IssuedAt = AsUtc(r.IssuedAt),
                    Amount = Round2(r.NetAmount),
                    TaxAmount = Round2(r.TaxAmount),
                    GrossAmount = Round2(r.GrossAmount),
                    TaxRate = rate,
                    TaxGroupName = ResolveGroupName(nameByRate, rate),
                    TseSignature = tse,
                };
            })
            .OrderBy(t => t.IssuedAt)
            .ThenBy(t => t.ReceiptNumber)
            .ThenBy(t => t.TaxRate)
            .ToList();

        var taxBreakdown = transactions
            .GroupBy(t => t.TaxRate)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => Round2(g.Sum(t => t.TaxAmount)));

        var warnings = BuildComplianceWarnings(rows.Select(r => (
            r.ReceiptId,
            r.ReceiptNumber,
            r.TaxTotal,
            (string?)r.PaymentTse,
            (string?)r.SignatureValue,
            LineTax: r.TaxAmount)).ToList());

        var report = new RksvReport
        {
            PeriodStart = rangeStart,
            PeriodEnd = rangeEnd,
            Transactions = transactions,
            TaxBreakdown = taxBreakdown,
            TotalNet = Round2(transactions.Sum(t => t.Amount)),
            TotalTax = Round2(transactions.Sum(t => t.TaxAmount)),
            TotalGross = Round2(transactions.Sum(t => t.GrossAmount)),
            IsCompliant = warnings.Count == 0,
            Warnings = warnings,
        };

        _logger.LogInformation(
            "RKSV historical report tenant {TenantId}: {TxnCount} tax lines, tax={Tax} compliant={Compliant} ({From:o}–{To:o})",
            tenantId,
            transactions.Count,
            report.TotalTax,
            report.IsCompliant,
            rangeStart,
            rangeEnd);

        return report;
    }

    public async Task<TaxBreakdown> GetTaxBreakdownForPeriodAsync(
        Guid tenantId,
        DateTime dateUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var day = AsUtc(dateUtc).Date;
        var rangeStart = DateTime.SpecifyKind(day, DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddDays(1);

        var report = await GenerateHistoricalReportAsync(tenantId, rangeStart, rangeEnd, cancellationToken)
            .ConfigureAwait(false);

        return new TaxBreakdown
        {
            Date = rangeStart,
            PeriodStart = rangeStart,
            PeriodEnd = rangeEnd,
            ByRate = report.TaxBreakdown,
            TotalNet = report.TotalNet,
            TotalTax = report.TotalTax,
            TotalGross = report.TotalGross,
            ReceiptCount = report.Transactions.Select(t => t.ReceiptId).Distinct().Count(),
        };
    }

    public async Task<PriceHistoryReport> GetPriceHistoryForProductAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var history = await _db.ProductPriceHistories.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.ProductId == productId)
            .OrderByDescending(h => h.EffectiveFrom)
            .Select(h => new PriceHistoryReportEntry
            {
                Id = h.Id,
                OldPrice = h.OldPrice,
                NewPrice = h.NewPrice,
                OldTaxGroupId = h.OldTaxGroupId,
                NewTaxGroupId = h.NewTaxGroupId,
                OldTaxRate = h.OldTaxRate,
                NewTaxRate = h.NewTaxRate,
                EffectiveFrom = h.EffectiveFrom,
                EffectiveTo = h.EffectiveTo,
                IsActive = h.IsActive,
                Reason = h.Reason,
                IsRksvCompliant = h.IsRksvCompliant,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var versions = await _db.ProductPriceVersions.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.ProductId == productId)
            .OrderByDescending(v => v.ValidFrom)
            .Select(v => new PriceVersionReportEntry
            {
                Id = v.Id,
                Price = v.Price,
                TaxGroupId = v.TaxGroupId,
                TaxGroupName = v.TaxGroup != null ? v.TaxGroup.Name : null,
                ValidFrom = v.ValidFrom,
                ValidTo = v.ValidTo,
                IsCurrent = v.IsCurrent,
                Version = v.Version ?? string.Empty,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PriceHistoryReport
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CatalogVersion = product.Version <= 0 ? 1 : product.Version,
            OriginalProductId = product.OriginalProductId,
            IsArchived = !product.IsActive || product.ArchivedAt is not null,
            History = history,
            Versions = versions,
        };
    }

    private static List<ComplianceWarning> BuildComplianceWarnings(
        IReadOnlyList<(Guid ReceiptId, string ReceiptNumber, decimal TaxTotal, string? PaymentTse, string? SignatureValue, decimal LineTax)> rows)
    {
        var warnings = new List<ComplianceWarning>();

        foreach (var group in rows.GroupBy(r => new { r.ReceiptId, r.ReceiptNumber, r.TaxTotal, r.PaymentTse, r.SignatureValue }))
        {
            var tse = FirstNonEmpty(group.Key.PaymentTse, group.Key.SignatureValue);
            if (string.IsNullOrWhiteSpace(tse))
            {
                warnings.Add(new ComplianceWarning
                {
                    Code = "MISSING_TSE_SIGNATURE",
                    Message = "Receipt has no TSE / RKSV signature.",
                    ReceiptId = group.Key.ReceiptId,
                    ReceiptNumber = group.Key.ReceiptNumber,
                });
            }

            var lineTaxSum = Round2(group.Sum(x => x.LineTax));
            var headerTax = Round2(group.Key.TaxTotal);
            if (headerTax != 0m && Math.Abs(lineTaxSum - headerTax) > 0.02m)
            {
                warnings.Add(new ComplianceWarning
                {
                    Code = "TAX_LINE_MISMATCH",
                    Message =
                        $"Receipt tax total ({headerTax.ToString("0.00", CultureInfo.InvariantCulture)}) " +
                        $"does not match sum of historical tax lines ({lineTaxSum.ToString("0.00", CultureInfo.InvariantCulture)}).",
                    ReceiptId = group.Key.ReceiptId,
                    ReceiptNumber = group.Key.ReceiptNumber,
                });
            }
        }

        return warnings;
    }

    private async Task<Dictionary<decimal, string>> LoadTaxGroupNamesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var groups = await _db.TaxGroups.AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .Select(g => new { g.Rate, g.Name, g.IsDefault, g.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Prefer active/default names for display labels; rates on lines remain authoritative.
        return groups
            .GroupBy(g => Round2(g.Rate))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.IsDefault)
                    .ThenBy(x => x.Name)
                    .First().Name);
    }

    private static string ResolveGroupName(IReadOnlyDictionary<decimal, string> nameByRate, decimal rate) =>
        nameByRate.TryGetValue(rate, out var name)
            ? name
            : rate switch
            {
                20m => "Normalsatz",
                13m => "Mittelsteuersatz",
                10m => "Ermäßigt",
                4.9m => "Ermäßigt (Neu)",
                0m => "Nullsteuersatz",
                _ => $"{rate.ToString("0.##", CultureInfo.InvariantCulture)}%",
            };

    private static void ValidatePeriod(Guid tenantId, DateTime start, DateTime end)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (end <= start)
            throw new ArgumentException("Period end must be after period start.");
        if ((end - start).TotalDays > 366)
            throw new ArgumentException("RKSV report period cannot exceed 366 days.");
    }

    private static (DateTime From, DateTime To) NormalizeUtcRange(DateTime start, DateTime end)
    {
        return (AsUtc(start), AsUtc(end));
    }

    private static DateTime AsUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }
}
