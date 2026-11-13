using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services.Countries.Strategies.Austria;

/// <summary>
/// Austrian tax adapter. Every number comes from <see cref="CartMoneyHelper"/> (line math, rounding,
/// tax summary) or <see cref="RksvTaxSetMapper"/> (RKSV gross buckets). Nothing is recomputed here, so
/// the live RKSV output stays identical.
///
/// Stateless — registered as a singleton. VAT-ID shape goes through <see cref="IVatIdValidator"/>
/// (no VIES on this path).
/// </summary>
public sealed class AustriaTaxStrategy : ITaxStrategy
{
    /// <summary>
    /// Austrian receipts (Kleinbetragsrechnung) never carry a customer UID, so the live path does not
    /// require one. Cross-border rules are planned — see <c>docs/EINVOICING_EU.md</c>.
    /// </summary>
    private const bool AustrianReceiptsRequireCustomerVatId = false;

    private readonly IVatIdValidator _vatIdValidator;

    /// <summary>Test and singleton fallback: shape-only validator, VIES client never invoked.</summary>
    [ActivatorUtilitiesConstructor]
    public AustriaTaxStrategy()
        : this(new VatIdValidator(new DisabledViesClient()))
    {
    }

    public AustriaTaxStrategy(IVatIdValidator vatIdValidator)
    {
        _vatIdValidator = vatIdValidator ?? throw new ArgumentNullException(nameof(vatIdValidator));
    }

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

        // Shape only — VIES is not part of the live payment UID gate. The validator reads the
        // profile seed; this class still has no regex of its own.
        return _vatIdValidator.Validate(vatId, profile);
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
