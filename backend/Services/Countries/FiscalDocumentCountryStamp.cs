using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Freezes the operating country and VAT regime onto a fiscal document at issue time.
/// Country change must never rewrite these snapshots.
/// </summary>
public static class FiscalDocumentCountryStamp
{
    public static void Apply(PaymentDetails target, CountryStrategyBinding binding)
        => Apply(target, binding.Profile.Code, binding.VatRegime);

    public static void Apply(Invoice target, CountryStrategyBinding binding)
        => Apply(target, binding.Profile.Code, binding.VatRegime);

    public static void Apply(Receipt target, CountryStrategyBinding binding)
        => Apply(target, binding.Profile.Code, binding.VatRegime);

    public static void Apply(PaymentDetails target, string? countryCode, VatRegime vatRegime)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.CountryCodeAtIssue = NormalizeCountry(countryCode);
        target.VatRegimeAtIssue = vatRegime;
    }

    public static void Apply(Invoice target, string? countryCode, VatRegime vatRegime)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.CountryCodeAtIssue = NormalizeCountry(countryCode);
        target.VatRegimeAtIssue = vatRegime;
    }

    public static void Apply(Receipt target, string? countryCode, VatRegime vatRegime)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.CountryCodeAtIssue = NormalizeCountry(countryCode);
        target.VatRegimeAtIssue = vatRegime;
    }

    public static void CopyFromPayment(
        Invoice target,
        PaymentDetails payment,
        CountryStrategyBinding? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payment);
        if (TryCopyFromPayment(payment, out var country, out var regime))
        {
            target.CountryCodeAtIssue = country;
            target.VatRegimeAtIssue = regime;
            return;
        }

        if (fallback != null)
            Apply(target, fallback);
    }

    public static void CopyFromPayment(
        Receipt target,
        PaymentDetails payment,
        CountryStrategyBinding? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payment);
        if (TryCopyFromPayment(payment, out var country, out var regime))
        {
            target.CountryCodeAtIssue = country;
            target.VatRegimeAtIssue = regime;
            return;
        }

        if (fallback != null)
            Apply(target, fallback);
    }

    public static void CopyFromInvoice(
        Invoice target,
        Invoice original,
        CountryStrategyBinding? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(original);
        if (!string.IsNullOrWhiteSpace(original.CountryCodeAtIssue))
        {
            target.CountryCodeAtIssue = NormalizeCountry(original.CountryCodeAtIssue);
            target.VatRegimeAtIssue = original.VatRegimeAtIssue;
            return;
        }

        if (fallback != null)
            Apply(target, fallback);
    }

    /// <summary>
    /// Counts invoice, receipt, and payment_details rows for the tenant. Used by the
    /// historical-preservation audit; never used to UPDATE those rows.
    /// </summary>
    public static async Task<int> CountHistoricalDocumentsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var invoices = await db.Invoices.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var receipts = await db.Receipts.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var payments = await (
                from p in db.PaymentDetails.AsNoTracking().IgnoreQueryFilters()
                join cr in db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                    on p.CashRegisterId equals cr.Id
                where cr.TenantId == tenantId
                select p.Id)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        return invoices + receipts + payments;
    }

    public static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;
        var trimmed = countryCode.Trim().ToUpperInvariant();
        return trimmed.Length <= 2 ? trimmed : trimmed[..2];
    }

    private static bool TryCopyFromPayment(
        PaymentDetails payment,
        out string? country,
        out VatRegime? regime)
    {
        country = NormalizeCountry(payment.CountryCodeAtIssue);
        regime = payment.VatRegimeAtIssue;
        return country != null || regime != null;
    }
}
