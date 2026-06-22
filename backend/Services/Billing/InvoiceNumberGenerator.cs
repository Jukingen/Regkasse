using System.Globalization;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

public interface IInvoiceNumberGenerator
{
    string GenerateInvoiceNumber(DateTime date);
    (int Year, int Month, int Sequence) ParseInvoiceNumber(string invoiceNumber);
}

/// <summary>
/// Super Admin license billing invoice numbers: <c>RE{yyyy}{MM}{sequence}</c>
/// (e.g. <c>RE20260841</c> = 2026, August, sequence 41). Sequence resets each calendar month.
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private const string Prefix = "RE";
    private readonly AppDbContext _db;

    public InvoiceNumberGenerator(AppDbContext db) => _db = db;

    public string GenerateInvoiceNumber(DateTime date)
    {
        var (year, month) = ResolveYearMonth(date);
        var prefix = FormatPrefix(year, month);

        using var transaction = _db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            var nextSequence = GetNextSequence(prefix);
            transaction.Commit();
            return FormatInvoiceNumber(year, month, nextSequence);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public (int Year, int Month, int Sequence) ParseInvoiceNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)
            || !invoiceNumber.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid invoice number format.", nameof(invoiceNumber));
        }

        var numericPart = invoiceNumber[Prefix.Length..];
        if (numericPart.Length < 7)
            throw new ArgumentException("Invalid invoice number format.", nameof(invoiceNumber));

        if (!int.TryParse(numericPart.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(numericPart.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(numericPart[6..], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence < 1
            || month is < 1 or > 12)
        {
            throw new ArgumentException("Invalid invoice number format.", nameof(invoiceNumber));
        }

        return (year, month, sequence);
    }

    internal static string FormatPrefix(int year, int month) =>
        FormattableString.Invariant($"{Prefix}{year:D4}{month:D2}");

    internal static string FormatInvoiceNumber(int year, int month, int sequence)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Invoice sequence must be at least 1.");

        return FormattableString.Invariant($"{FormatPrefix(year, month)}{sequence}");
    }

    internal static bool TryParseSequence(string invoiceNumber, string prefix, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrEmpty(invoiceNumber)
            || !invoiceNumber.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = invoiceNumber[prefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
               && sequence > 0;
    }

    private static (int Year, int Month) ResolveYearMonth(DateTime date)
    {
        var utc = date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        };

        return (utc.Year, utc.Month);
    }

    private int GetNextSequence(string prefix)
    {
        var existing = _db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.InvoiceNumber.StartsWith(prefix))
            .Select(s => s.InvoiceNumber)
            .ToList();

        if (existing.Count == 0)
            return 1;

        var max = 0;
        foreach (var number in existing)
        {
            if (TryParseSequence(number, prefix, out var seq) && seq > max)
                max = seq;
        }

        return max + 1;
    }
}
