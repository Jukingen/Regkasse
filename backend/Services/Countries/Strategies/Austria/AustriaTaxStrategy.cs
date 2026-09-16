using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies.Austria;

/// <summary>
/// Austrian tax adapter. Every number comes from <see cref="CartMoneyHelper"/> (line math, rounding,
/// tax summary) or <see cref="RksvTaxSetMapper"/> (RKSV gross buckets). Nothing is recomputed here, so
/// the live RKSV output stays identical.
///
/// Stateless — registered as a singleton.
/// </summary>
public sealed class AustriaTaxStrategy : ITaxStrategy
{
    /// <summary>
    /// Austrian receipts (Kleinbetragsrechnung) never carry a customer UID, so the live path does not
    /// require one. Cross-border rules are planned — see <c>docs/EINVOICING_EU.md</c>.
    /// </summary>
    private const bool AustrianReceiptsRequireCustomerVatId = false;

    public string CountryCode => CountryProfileCodes.Austria;

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>();

        foreach (var item in lineItems)
        {
            var line = item switch
            {
                { TaxType: int taxType } =>
                    CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, taxType),
                { VatRatePercent: decimal vatRatePercent } =>
                    CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, vatRatePercent),
                _ => throw new ArgumentException(
                    "Line item must carry either a tax type or a VAT percent.",
                    nameof(lineItems)),
            };

            lines.Add(line);

            // Same accumulation as the live payment path: key = RKSV tax type, value = summed line VAT.
            var taxKey = line.TaxType.ToString().ToLowerInvariant();
            taxDetails[taxKey] = taxDetails.TryGetValue(taxKey, out var current)
                ? current + line.LineTax
                : line.LineTax;
        }

        var (totals, _) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown(lines);

        return new TaxCalculationResult
        {
            Lines = lines,
            TaxSummary = CartMoneyHelper.BuildTaxSummaryFromLines(lines),
            Totals = totals,
            TaxDetails = taxDetails,
        };
    }

    public VatIdValidationResult ValidateVatId(string? vatId, CountryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Strict on purpose: the live fiscal gates match the raw value, so this must not accept
        // whitespace or lower case that they reject. Pattern comes from the profile seed only —
        // no second UID regex in this class.
        return profile.MatchesVatIdShape(vatId)
            ? VatIdValidationResult.Valid(vatId!)
            : VatIdValidationResult.Invalid();
    }

    public InvoiceFieldRequirements DetermineInvoiceFields(CompanySettings company, Customer? customer)
    {
        ArgumentNullException.ThrowIfNull(company);

        return new InvoiceFieldRequirements
        {
            SellerVatId = company.VatId,
            SellerCountry = company.Country,
            BillingCountry = company.BillingCountry,
            VatRegime = company.VatRegime,
            TaxExempt = company.TaxExempt,
            CustomerVatId = string.IsNullOrWhiteSpace(customer?.TaxNumber) ? null : customer.TaxNumber.Trim(),
            RequiresCustomerVatId = AustrianReceiptsRequireCustomerVatId,
        };
    }

    public RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount) =>
        RksvTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, totalAmount);
}
