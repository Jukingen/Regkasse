using System.Globalization;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds MwSt analytics from <see cref="ReceiptTaxLine"/> joined to <see cref="Receipt.IssuedAt"/>.
/// </summary>
public sealed class TaxReportService : ITaxReportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TaxReportService> _logger;

    public TaxReportService(AppDbContext db, ILogger<TaxReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TaxReport> GetReportAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(tenantId, periodStartUtc, periodEndUtc);
        var (rangeStart, rangeEnd) = NormalizeUtcRange(periodStartUtc, periodEndUtc);

        var lines = await (
                from line in _db.ReceiptTaxLines.AsNoTracking()
                join receipt in _db.Receipts.AsNoTracking() on line.ReceiptId equals receipt.ReceiptId
                where line.TenantId == tenantId
                      && receipt.TenantId == tenantId
                      && receipt.IssuedAt >= rangeStart
                      && receipt.IssuedAt < rangeEnd
                select new
                {
                    line.TaxRate,
                    line.NetAmount,
                    line.TaxAmount,
                    line.GrossAmount,
                    line.ReceiptId,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nameByRate = await LoadTaxGroupNamesAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var groups = lines
            .GroupBy(l => decimal.Round(l.TaxRate, 2, MidpointRounding.AwayFromZero))
            .Select(g => new TaxGroupSummary
            {
                Rate = g.Key,
                TaxGroupName = ResolveGroupName(nameByRate, g.Key),
                NetRevenue = Round2(g.Sum(x => x.NetAmount)),
                TaxAmount = Round2(g.Sum(x => x.TaxAmount)),
                GrossRevenue = Round2(g.Sum(x => x.GrossAmount)),
                TransactionCount = g.Select(x => x.ReceiptId).Distinct().Count(),
            })
            .OrderBy(g => g.Rate)
            .ToList();

        var report = new TaxReport
        {
            PeriodStart = rangeStart,
            PeriodEnd = rangeEnd,
            TaxGroups = groups,
            TotalNetRevenue = Round2(groups.Sum(g => g.NetRevenue)),
            TotalTaxAmount = Round2(groups.Sum(g => g.TaxAmount)),
            TotalGrossRevenue = Round2(groups.Sum(g => g.GrossRevenue)),
        };

        _logger.LogInformation(
            "Tax report for tenant {TenantId}: {Groups} groups, tax={Tax} ({From:o}–{To:o})",
            tenantId,
            groups.Count,
            report.TotalTaxAmount,
            rangeStart,
            rangeEnd);

        return report;
    }

    public async Task<IReadOnlyList<TaxTrendPoint>> GetTrendAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        string granularity = "day",
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(tenantId, periodStartUtc, periodEndUtc);
        var (rangeStart, rangeEnd) = NormalizeUtcRange(periodStartUtc, periodEndUtc);
        var byMonth = string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase);

        var rows = await (
                from line in _db.ReceiptTaxLines.AsNoTracking()
                join receipt in _db.Receipts.AsNoTracking() on line.ReceiptId equals receipt.ReceiptId
                where line.TenantId == tenantId
                      && receipt.TenantId == tenantId
                      && receipt.IssuedAt >= rangeStart
                      && receipt.IssuedAt < rangeEnd
                select new { line.TaxRate, line.TaxAmount, receipt.IssuedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var points = rows
            .GroupBy(r =>
            {
                var issued = r.IssuedAt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(r.IssuedAt, DateTimeKind.Utc)
                    : r.IssuedAt.ToUniversalTime();
                var bucket = byMonth
                    ? new DateTime(issued.Year, issued.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(issued.Date, DateTimeKind.Utc);
                var rate = decimal.Round(r.TaxRate, 2, MidpointRounding.AwayFromZero);
                return (bucket, rate);
            })
            .Select(g => new TaxTrendPoint
            {
                Date = g.Key.bucket,
                Rate = g.Key.rate,
                TaxRateLabel = $"{g.Key.rate.ToString("0.##", CultureInfo.InvariantCulture)}%",
                Amount = Round2(g.Sum(x => x.TaxAmount)),
            })
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Rate)
            .ToList();

        return points;
    }

    public async Task<byte[]> ExportCsvAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(tenantId, periodStartUtc, periodEndUtc, cancellationToken)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Steuergruppe;Satz_Prozent;Netto;Steuer;Brutto;Belege");
        foreach (var g in report.TaxGroups)
        {
            sb.Append(EscapeCsv(g.TaxGroupName)).Append(';')
                .Append(g.Rate.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
                .Append(g.NetRevenue.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                .Append(g.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                .Append(g.GrossRevenue.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                .Append(g.TransactionCount)
                .AppendLine();
        }

        sb.Append("SUMME;;")
            .Append(report.TotalNetRevenue.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(report.TotalTaxAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(report.TotalGrossRevenue.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .AppendLine();

        sb.Append("PeriodStartUTC;").AppendLine(report.PeriodStart.ToString("o"));
        sb.Append("PeriodEndUTC;").AppendLine(report.PeriodEnd.ToString("o"));

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private async Task<Dictionary<decimal, string>> LoadTaxGroupNamesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var groups = await _db.TaxGroups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.IsActive)
            .Select(g => new { g.Rate, g.Name, g.IsDefault })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return groups
            .GroupBy(g => decimal.Round(g.Rate, 2, MidpointRounding.AwayFromZero))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).First().Name);
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
            throw new ArgumentException("Tax report period cannot exceed 366 days.");
    }

    private static (DateTime From, DateTime To) NormalizeUtcRange(DateTime start, DateTime end)
    {
        static DateTime AsUtc(DateTime dt) =>
            dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            };

        return (AsUtc(start), AsUtc(end));
    }

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
