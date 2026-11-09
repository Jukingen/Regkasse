using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Aggregates sale rows from <c>payment_details</c> for a Vienna period.
/// Special receipts, refunds, and stornos are excluded. Does not read or write TSE payloads.
/// </summary>
public static class PeriodSalesSummaryAggregator
{
    public static async Task<PeriodSalesSummary> AggregateAsync(
        AppDbContext db,
        Guid? cashRegisterId,
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        string periodLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var query = db.PaymentDetails.AsNoTracking()
            .Where(p =>
                p.IsActive
                && !p.IsRefund
                && !p.IsStorno
                && (p.RksvSpecialReceiptKind == null || p.RksvSpecialReceiptKind == "")
                && p.CreatedAt >= fromUtcInclusive
                && p.CreatedAt < toUtcExclusive);

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var registerId = cashRegisterId.Value;
            query = query.Where(p => p.CashRegisterId == registerId);
        }
        else
        {
            query = query.Where(p => db.CashRegisters.Any(r => r.Id == p.CashRegisterId));
        }

        var entities = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        var rows = entities
            .Select(p => new SaleRow(p.TotalAmount, p.TaxAmount, p.PaymentMethodRaw, p.TaxDetails))
            .ToList();

        return Build(periodLabel, rows);
    }

    internal static PeriodSalesSummary Build(string periodLabel, IReadOnlyList<SaleRow> rows)
    {
        decimal gross = 0m;
        decimal tax = 0m;
        decimal vat10 = 0m;
        decimal vat20 = 0m;
        decimal vat13 = 0m;
        decimal vat49 = 0m;
        var methods = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            gross += row.TotalAmount;
            tax += row.TaxAmount;

            var buckets = ReadTaxBuckets(row.TaxDetails);
            if (buckets.Count == 0 && row.TaxAmount != 0m)
                buckets[TaxTypes.Standard] = row.TaxAmount;

            vat20 += buckets.GetValueOrDefault(TaxTypes.Standard);
            vat10 += buckets.GetValueOrDefault(TaxTypes.Reduced);
            vat13 += buckets.GetValueOrDefault(TaxTypes.Special);
            vat49 += buckets.GetValueOrDefault(TaxTypes.ReducedNew);

            var label = PaymentMethodLabelDe(row.PaymentMethodRaw);
            methods[label] = methods.GetValueOrDefault(label) + row.TotalAmount;
        }

        EnsureMethod(methods, "Bar");
        EnsureMethod(methods, "Karte");

        var ordered = methods
            .OrderBy(pair => MethodSort(pair.Key))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PeriodSalesPaymentTotal
            {
                Label = pair.Key,
                Amount = Round2(pair.Value),
            })
            .ToList();

        var grossRounded = Round2(gross);
        return new PeriodSalesSummary
        {
            PeriodLabel = periodLabel,
            ReceiptCount = rows.Count,
            TotalRevenue = grossRounded,
            NetAmount = Round2(gross - tax),
            Vat10 = Round2(vat10),
            Vat20 = Round2(vat20),
            Vat13 = Round2(vat13),
            Vat49 = Round2(vat49),
            GrossAmount = grossRounded,
            PaymentMethods = ordered,
        };
    }

    public static string MonthLabel(int year, int month)
    {
        var culture = CultureInfo.GetCultureInfo("de-AT");
        var name = culture.DateTimeFormat.GetMonthName(month);
        if (string.IsNullOrWhiteSpace(name))
            name = month.ToString("00", CultureInfo.InvariantCulture);
        return $"Monat: {name} {year}";
    }

    public static string DayLabel(DateTime day) =>
        $"Tag: {day:dd.MM.yyyy}";

    public static string YearLabel(int year) => $"Jahr: {year}";

    internal static string PaymentMethodLabelDe(string? raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && Enum.IsDefined(typeof(PaymentMethod), n))
        {
            return ((PaymentMethod)n) switch
            {
                PaymentMethod.Cash => "Bar",
                PaymentMethod.Card => "Karte",
                PaymentMethod.BankTransfer => "Überweisung",
                PaymentMethod.Check => "Scheck",
                PaymentMethod.Voucher => "Gutschein",
                PaymentMethod.Mobile => "Mobil",
                _ => raw?.Trim() ?? "—",
            };
        }

        return string.IsNullOrWhiteSpace(raw) ? "—" : raw.Trim();
    }

    private static void EnsureMethod(Dictionary<string, decimal> methods, string label)
    {
        if (!methods.ContainsKey(label))
            methods[label] = 0m;
    }

    private static int MethodSort(string label) =>
        label switch
        {
            "Bar" => 0,
            "Karte" => 1,
            _ => 2,
        };

    private static Dictionary<int, decimal> ReadTaxBuckets(JsonDocument? taxDetails)
    {
        var buckets = new Dictionary<int, decimal>();
        if (taxDetails is null || taxDetails.RootElement.ValueKind != JsonValueKind.Object)
            return buckets;

        foreach (var prop in taxDetails.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetDecimal(out var amount))
                continue;

            if (!int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taxType))
                continue;

            if (taxType is not (TaxTypes.Standard or TaxTypes.Reduced or TaxTypes.Special or TaxTypes.ZeroRate or TaxTypes.ReducedNew))
                continue;

            buckets[taxType] = buckets.GetValueOrDefault(taxType) + amount;
        }

        return buckets;
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    internal readonly record struct SaleRow(
        decimal TotalAmount,
        decimal TaxAmount,
        string PaymentMethodRaw,
        JsonDocument? TaxDetails);
}
