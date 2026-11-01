using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Austrian RKSV BelegNr / invoice number.
/// <c>AT-{registerNumber}-{tenantId:N}-{yyyyMMdd}-{sequence}</c>.
/// Register numbers are unique only inside one tenant, and <c>invoices.InvoiceNumber</c> is unique
/// for the whole database, so the tenant id is part of the number.
/// </summary>
public static class AustrianBelegNumber
{
    /// <summary>Matches <see cref="Invoice.InvoiceNumberMaxLength"/>.</summary>
    public const int MaxLength = Invoice.InvoiceNumberMaxLength;

    public static string Format(string registerNumber, Guid tenantId, DateTime sequenceDate, int sequenceNumber)
    {
        if (string.IsNullOrWhiteSpace(registerNumber))
            throw new ArgumentException("Register number is required.", nameof(registerNumber));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required so the BelegNr is unique across tenants.", nameof(tenantId));
        if (sequenceNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence must be at least 1.");

        var number =
            $"AT-{registerNumber.Trim()}-{tenantId:N}-{sequenceDate:yyyyMMdd}-{sequenceNumber}";
        if (number.Length > MaxLength)
        {
            throw new InvalidOperationException(
                $"BelegNr length {number.Length} exceeds the invoice number limit of {MaxLength}.");
        }

        return number;
    }
}
