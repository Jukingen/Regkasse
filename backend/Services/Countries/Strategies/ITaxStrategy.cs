using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Country-specific tax behavior. Implementations are **adapters**: they select and delegate to the
/// services that already own the math. Austria must keep today's output byte for byte, so
/// <see cref="Austria.AustriaTaxStrategy"/> may not restate a rounding rule, a bucket rule, or a rate.
///
/// Resolved through <see cref="ITaxStrategyResolver"/>. No domain flow calls this yet — see
/// <c>docs/COUNTRIES.md</c> §3.
/// </summary>
public interface ITaxStrategy
{
    /// <summary>Profile code this strategy serves (<see cref="CountryProfileCodes"/>).</summary>
    string CountryCode { get; }

    /// <summary>Line and summary VAT for a sale. Rounding stays wherever the delegate does it.</summary>
    TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context);

    /// <summary>
    /// Shape-only VAT-ID check. The pattern must come from <paramref name="profile"/> —
    /// implementations must not carry their own regex.
    /// </summary>
    VatIdValidationResult ValidateVatId(string? vatId, CountryProfile profile);

    /// <summary>Which tax fields the document must carry, projected from existing settings.</summary>
    InvoiceFieldRequirements DetermineInvoiceFields(CompanySettings company, Customer? customer);

    /// <summary>
    /// Austrian RKSV <c>Betrag-Satz-*</c> gross buckets for a persisted
    /// <c>payment_details.tax_details</c> payload. Separate from <see cref="CalculateTax"/> because it
    /// is a downstream projection of stored data, not line-item math. Countries without a bucket model
    /// do not implement it.
    /// </summary>
    RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount);
}
