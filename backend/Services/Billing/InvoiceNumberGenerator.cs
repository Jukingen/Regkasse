using System.Globalization;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

public interface IInvoiceNumberGenerator
{
    string GenerateInvoiceNumber(DateTime date);
}

/// <summary>
/// Super Admin license billing invoice numbers: <c>RE{yyyy}{MM}{sequence}</c>
/// (e.g. <c>RE20260841</c> = 2026, August, sequence 41). Sequence resets each calendar month.
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
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

    internal static string FormatPrefix(int year, int month) =>
        FormattableString.Invariant($"RE{year:D4}{month:D2}");

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
