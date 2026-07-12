using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Builds yearly RKSV closing totals from persisted Monatsbeleg rows.</summary>
public static class JahresbelegYearlyAggregator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<JahresbelegSummaryDto> AggregateAsync(
        IQueryable<Monatsbeleg> monatsbelegQuery,
        Guid cashRegisterId,
        int year,
        bool decemberMonatsbelegCountsAsJahresbeleg,
        CancellationToken cancellationToken = default)
    {
        var monthlyRows = await monatsbelegQuery
            .AsNoTracking()
            .Where(m => m.CashRegisterId == cashRegisterId && m.Year == year)
            .OrderBy(m => m.Month)
            .ToListAsync(cancellationToken);

        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var requiredMonthCount = year < viennaYear
            ? 12
            : year > viennaYear
                ? 0
                : viennaMonth;

        var presentMonths = monthlyRows.Select(m => m.Month).ToHashSet();
        var missingMonths = Enumerable.Range(1, requiredMonthCount)
            .Where(m => !presentMonths.Contains(m))
            .ToList();

        var decemberRow = monthlyRows.FirstOrDefault(m => m.Month == 12);
        var isDecemberMonatsbeleg = decemberMonatsbelegCountsAsJahresbeleg && decemberRow != null;

        var references = monthlyRows
            .Select(m => new JahresbelegMonthlyReferenceDto
            {
                Year = m.Year,
                Month = m.Month,
                Id = m.Id,
            })
            .ToList();

        var txBreakdown = new TransactionBreakdown();
        foreach (var row in monthlyRows)
        {
            // Monatsbeleg entity stores payment totals but not tx breakdown counts; derive from gross split.
            if (row.TotalCash > 0m)
                txBreakdown.Cash++;
            if (row.TotalCard > 0m)
                txBreakdown.Card++;
            if (row.TotalVoucher > 0m)
                txBreakdown.Voucher++;
            txBreakdown.Total += row.TransactionCount;
        }

        var totalCash = monthlyRows.Sum(m => m.TotalCash);
        var totalCard = monthlyRows.Sum(m => m.TotalCard);
        var totalVoucher = monthlyRows.Sum(m => m.TotalVoucher);
        var totalOther = monthlyRows.Sum(m => m.TotalOther);

        return new JahresbelegSummaryDto
        {
            Year = year,
            CashRegisterId = cashRegisterId,
            MonatsbelegCount = monthlyRows.Count,
            MonthlyReferences = references,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalVoucher = totalVoucher,
            TotalOther = totalOther,
            TotalGross = monthlyRows.Sum(m => m.TotalGross),
            TotalTax = monthlyRows.Sum(m => m.TotalTax),
            TaxRate20 = monthlyRows.Sum(m => m.TaxRate20),
            TaxRate10 = monthlyRows.Sum(m => m.TaxRate10),
            TaxRate0 = monthlyRows.Sum(m => m.TaxRate0),
            TransactionCount = monthlyRows.Sum(m => m.TransactionCount),
            PaymentBreakdown = PaymentBreakdown.FromAmounts(totalCash, totalCard, totalVoucher, totalOther),
            TaxBreakdown = new DailyClosingTaxBreakdownDto
            {
                TaxAt20 = monthlyRows.Sum(m => m.TaxRate20),
                TaxAt10 = monthlyRows.Sum(m => m.TaxRate10),
                GrossAt0 = monthlyRows.Sum(m => m.TaxRate0),
            },
            TransactionBreakdown = txBreakdown,
            IsDecemberMonatsbeleg = isDecemberMonatsbeleg,
            MissingMonths = missingMonths,
        };
    }

    public static string SerializeMonthlyReferences(IEnumerable<JahresbelegMonthlyReferenceDto> references) =>
        JsonSerializer.Serialize(references, JsonOptions);

    public static IReadOnlyList<JahresbelegMonthlyReferenceDto> DeserializeMonthlyReferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<JahresbelegMonthlyReferenceDto>();

        try
        {
            return JsonSerializer.Deserialize<List<JahresbelegMonthlyReferenceDto>>(json, JsonOptions)
                   ?? new List<JahresbelegMonthlyReferenceDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<JahresbelegMonthlyReferenceDto>();
        }
    }
}
