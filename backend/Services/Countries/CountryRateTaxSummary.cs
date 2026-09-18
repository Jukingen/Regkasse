using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Tax summary grouped by the line's actual rate fraction. Does not use RKSV
/// <see cref="CartMoneyHelper.BuildTaxSummaryFromLines"/> (that helper reads AT <c>TaxTypes</c>).
/// </summary>
public static class CountryRateTaxSummary
{
    public static IReadOnlyList<CartMoneyHelper.TaxSummaryLine> FromLineRates(
        IEnumerable<CartMoneyHelper.LineAmounts> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines
            .GroupBy(l => l.TaxRate)
            .Select(g => new CartMoneyHelper.TaxSummaryLine(
                TaxType: 0,
                TaxRatePct: g.Key * 100m,
                NetAmount: g.Sum(x => x.LineNet),
                TaxAmount: g.Sum(x => x.LineTax),
                GrossAmount: g.Sum(x => x.LineGross)))
            .OrderBy(t => t.TaxRatePct)
            .ToList();
    }

    public static CartMoneyHelper.ReceiptTotals Totals(IReadOnlyList<CartMoneyHelper.LineAmounts> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return new CartMoneyHelper.ReceiptTotals(
            lines.Sum(l => l.LineNet),
            lines.Sum(l => l.LineTax),
            lines.Sum(l => l.LineGross));
    }
}
