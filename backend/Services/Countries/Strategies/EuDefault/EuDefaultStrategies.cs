using System.Globalization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies.EuDefault;

/// <summary>
/// EU_DEFAULT tax shape: reverse charge and NON_EU at 0%, OSS at the line rate.
/// Line math from <see cref="CartMoneyHelper.ComputeLine(decimal, int, decimal)"/>, summary from
/// <see cref="CountryRateTaxSummary"/> (not AT <c>TaxTypes</c> buckets). No Peppol / ViDA submission.
/// </summary>
public sealed class EuDefaultTaxStrategy : ITaxStrategy
{
    private static readonly string ZeroRateKey = 0m.ToString(CultureInfo.InvariantCulture);

    private readonly ICountryProfileRegistry _profiles;
    private readonly IVatIdValidator _vatIdValidator;
    private readonly IFeatureFlagService? _featureFlags;

    public EuDefaultTaxStrategy()
        : this(new CountryProfileRegistry(), new VatIdValidator(new DisabledViesClient()), featureFlags: null)
    {
    }

    public EuDefaultTaxStrategy(
        ICountryProfileRegistry profiles,
        IVatIdValidator vatIdValidator,
        IFeatureFlagService? featureFlags = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _vatIdValidator = vatIdValidator ?? throw new ArgumentNullException(nameof(vatIdValidator));
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.EuDefault;

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentNullException.ThrowIfNull(context);
        EnsureEnabled();

        return context.VatRegime switch
        {
            VatRegime.EU_REVERSE_CHARGE => CalculateReverseCharge(lineItems, context),
            VatRegime.NON_EU => CalculateZeroRated(lineItems),
            VatRegime.EU_OSS => CalculateOss(lineItems),
            _ => throw new ArgumentException(
                $"EU_DEFAULT does not calculate tax for regime {context.VatRegime}.",
                nameof(context)),
        };
    }

    public VatIdValidationResult ValidateVatId(string? vatId, CountryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureEnabled();
        return _vatIdValidator.Validate(vatId, profile);
    }

    public InvoiceFieldRequirements DetermineInvoiceFields(CompanySettings company, Customer? customer)
    {
        ArgumentNullException.ThrowIfNull(company);
        EnsureEnabled();

        return new InvoiceFieldRequirements
        {
            SellerVatId = company.VatId,
            SellerCountry = company.Country,
            BillingCountry = company.BillingCountry,
            VatRegime = company.VatRegime,
            TaxExempt = company.TaxExempt,
            CustomerVatId = string.IsNullOrWhiteSpace(customer?.TaxNumber) ? null : customer.TaxNumber.Trim(),
            RequiresCustomerVatId = company.VatRegime == VatRegime.EU_REVERSE_CHARGE,
        };
    }

    public RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount) =>
        throw new NotImplementedException(
            $"{CountryCode} tax behavior is not implemented (ProjectFiscalTaxSets). See {CountryStrategyDocs.EuDefault}.");

    private TaxCalculationResult CalculateReverseCharge(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        var buyerVatId = context.BuyerVatId;
        var buyerProfile = ResolveBuyerVatProfile(buyerVatId, context.CountryProfile);
        if (string.IsNullOrEmpty(buyerVatId) || !_vatIdValidator.Validate(buyerVatId, buyerProfile).IsValid)
            throw new VatIdShapeInvalidException();

        return CalculateZeroRated(lineItems);
    }

    private static TaxCalculationResult CalculateZeroRated(IReadOnlyList<TaxLineItemInput> lineItems)
    {
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in lineItems)
        {
            var line = CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, 0m);
            lines.Add(line);
            taxDetails[ZeroRateKey] = taxDetails.TryGetValue(ZeroRateKey, out var current)
                ? current + line.LineTax
                : line.LineTax;
        }

        return new TaxCalculationResult
        {
            Lines = lines,
            TaxSummary = CountryRateTaxSummary.FromLineRates(lines),
            Totals = CountryRateTaxSummary.Totals(lines),
            TaxDetails = taxDetails,
        };
    }

    private static TaxCalculationResult CalculateOss(IReadOnlyList<TaxLineItemInput> lineItems)
    {
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in lineItems)
        {
            if (item.VatRatePercent is not decimal percent)
            {
                throw new ArgumentException(
                    "EU OSS line items must use VAT percent, not RKSV tax types.",
                    nameof(lineItems));
            }

            var line = CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, percent);
            lines.Add(line);
            var key = percent.ToString(CultureInfo.InvariantCulture);
            taxDetails[key] = taxDetails.TryGetValue(key, out var current)
                ? current + line.LineTax
                : line.LineTax;
        }

        return new TaxCalculationResult
        {
            Lines = lines,
            TaxSummary = CountryRateTaxSummary.FromLineRates(lines),
            Totals = CountryRateTaxSummary.Totals(lines),
            TaxDetails = taxDetails,
        };
    }

    /// <summary>
    /// Two-letter prefix matching: AT / DE / CH use that seeded profile's regex. Anything else
    /// (including FR/IT) stays on <paramref name="contextProfile"/> — never
    /// <see cref="ICountryProfileRegistry.GetOrDefault"/>.
    /// </summary>
    private CountryProfile ResolveBuyerVatProfile(string? buyerVatId, CountryProfile contextProfile)
    {
        if (string.IsNullOrEmpty(buyerVatId) || buyerVatId.Length < 2)
            return contextProfile;

        var prefix = buyerVatId[..2];
        if (prefix is not (
            CountryProfileCodes.Austria
            or CountryProfileCodes.Germany
            or CountryProfileCodes.Switzerland))
        {
            return contextProfile;
        }

        return _profiles.TryGet(prefix, out var matched) ? matched : contextProfile;
    }

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingEn16931);
        }
    }
}

/// <summary>
/// EU_DEFAULT invoicing shape (EN 16931 disclosures + placeholder document). Numbering, Peppol,
/// and tax-authority submission stay unimplemented — see <c>docs/EINVOICING_EU.md</c>.
/// </summary>
public sealed class EuDefaultInvoiceStrategy : IInvoiceStrategy
{
    private const string LegalBasis = "EN 16931";

    private static readonly IReadOnlyList<DisclosureRequirement> CoreDisclosures =
    [
        new("seller.name", LegalBasis, nameof(CompanySettings.CompanyName)),
        new("seller.vatId", LegalBasis, nameof(CompanySettings.VatId)),
        new("buyer.name", LegalBasis, nameof(Customer.Name)),
        new("invoice.number", LegalBasis, nameof(PaymentDetails.ReceiptNumber)),
        new("invoice.date", LegalBasis, nameof(PaymentDetails.CreatedAt)),
        new("invoice.currency", LegalBasis, nameof(InvoiceDocumentDto.Currency)),
        new("invoice.lines", LegalBasis, nameof(TaxCalculationResult.Lines)),
        new("invoice.net", LegalBasis, nameof(PaymentDetails.TotalAmount)),
        new("invoice.tax", LegalBasis, nameof(PaymentDetails.TaxAmount)),
        new("invoice.gross", LegalBasis, nameof(PaymentDetails.TotalAmount)),
        new("invoice.taxBreakdown", LegalBasis, nameof(PaymentDetails.TaxDetails)),
    ];

    private static readonly DisclosureRequirement BuyerVatIdDisclosure =
        new("buyer.vatId", LegalBasis, nameof(Customer.TaxNumber));

    private readonly IFeatureFlagService? _featureFlags;

    public EuDefaultInvoiceStrategy(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.EuDefault;

    public Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            $"{CountryCode} invoicing behavior is not implemented (AllocateReceiptNumberAsync). See {CountryStrategyDocs.EuDefault}.");

    public Task<InvoiceDocument> BuildInvoiceDocumentAsync(
        PaymentDetails payment,
        CompanySettings company,
        Customer? customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(company);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureEnabled();

        var net = payment.TotalAmount - payment.TaxAmount;
        var structured = new InvoiceDocumentDto
        {
            CountryCode = CountryCode,
            SellerTaxNumber = company.CompanyTaxNumber,
            SellerVatId = company.VatId,
            InvoiceNumber = payment.ReceiptNumber,
            InvoiceDate = payment.CreatedAt,
            PerformanceDescription = payment.Notes,
            NetAmount = net,
            TaxAmount = payment.TaxAmount,
            GrossAmount = payment.TotalAmount,
            Currency = "EUR",
        };

        var receipt = new ReceiptDTO
        {
            ReceiptNumber = payment.ReceiptNumber,
            Date = payment.CreatedAt,
            SubTotal = net,
            TaxAmount = payment.TaxAmount,
            GrandTotal = payment.TotalAmount,
            Company = new ReceiptCompanyDTO
            {
                Name = company.CompanyName,
                Address = company.CompanyAddress,
                TaxNumber = company.CompanyTaxNumber,
            },
        };

        return Task.FromResult(new InvoiceDocument(CountryCode, receipt, structured));
    }

    public IReadOnlyList<DisclosureRequirement> GetMandatoryDisclosures(
        CompanySettings company,
        Customer? customer)
    {
        ArgumentNullException.ThrowIfNull(company);
        EnsureEnabled();

        if (company.VatRegime != VatRegime.EU_REVERSE_CHARGE)
            return CoreDisclosures;

        var withBuyerVat = new List<DisclosureRequirement>(CoreDisclosures.Count + 1);
        withBuyerVat.AddRange(CoreDisclosures);
        withBuyerVat.Add(BuyerVatIdDisclosure);
        return withBuyerVat;
    }

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingEn16931);
        }
    }
}
