using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

/// <summary>
/// German VAT shape: rates from <see cref="ICountryTaxTypeRegistry"/> (19/7), line math from
/// <see cref="CartMoneyHelper.ComputeLine(decimal, int, decimal)"/>, summary from
/// <see cref="CountryRateTaxSummary"/> (not AT <c>TaxTypes</c> buckets).
/// </summary>
public sealed class GermanyTaxStrategy : ITaxStrategy
{
    private readonly ICountryTaxTypeRegistry _taxTypes;
    private readonly IVatIdValidator _vatIdValidator;
    private readonly IFeatureFlagService? _featureFlags;

    public GermanyTaxStrategy()
        : this(new CountryTaxTypeRegistry(), new VatIdValidator(new DisabledViesClient()), featureFlags: null)
    {
    }

    public GermanyTaxStrategy(
        ICountryTaxTypeRegistry taxTypes,
        IVatIdValidator vatIdValidator,
        IFeatureFlagService? featureFlags = null)
    {
        _taxTypes = taxTypes ?? throw new ArgumentNullException(nameof(taxTypes));
        _vatIdValidator = vatIdValidator ?? throw new ArgumentNullException(nameof(vatIdValidator));
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.Germany;

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentNullException.ThrowIfNull(context);
        EnsureEnabled();

        var catalog = _taxTypes.Get(CountryProfileCodes.Germany);
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in lineItems)
        {
            if (item.VatRatePercent is not decimal percent)
            {
                throw new ArgumentException(
                    "DE line items must use VAT percent from CountryTaxType (19 or 7), not RKSV tax types.",
                    nameof(lineItems));
            }

            var taxType = catalog.FirstOrDefault(t => t.Rate == percent)
                ?? throw new ArgumentException(
                    $"VAT rate {percent} is not a seeded DE CountryTaxType.",
                    nameof(lineItems));

            var line = CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, percent);
            lines.Add(line);
            taxDetails[taxType.Code] = taxDetails.TryGetValue(taxType.Code, out var current)
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
            RequiresCustomerVatId = false,
        };
    }

    public RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount) =>
        throw new NotImplementedException(
            $"{CountryCode} tax behavior is not implemented (ProjectFiscalTaxSets). See {CountryStrategyDocs.Germany}.");

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalKassenSicherheitDe);
        }
    }
}

/// <summary>
/// German invoicing shape (UStG §14 disclosures + placeholder document). Numbering and TSE stay
/// unimplemented until call-site wiring (Paket 30-c).
/// </summary>
public sealed class GermanyInvoiceStrategy : IInvoiceStrategy
{
    private static readonly IReadOnlyList<DisclosureRequirement> UstgDisclosures =
    [
        new("seller.taxNumber", "UStG §14", nameof(CompanySettings.CompanyTaxNumber)),
        new("seller.vatId", "UStG §14", nameof(CompanySettings.VatId)),
        new("invoice.number", "UStG §14", nameof(PaymentDetails.ReceiptNumber)),
        new("invoice.date", "UStG §14", nameof(PaymentDetails.CreatedAt)),
        new("invoice.performanceDescription", "UStG §14", "Leistungsbeschreibung"),
        new("invoice.net", "UStG §14", nameof(PaymentDetails.TotalAmount)),
        new("invoice.tax", "UStG §14", nameof(PaymentDetails.TaxAmount)),
        new("invoice.gross", "UStG §14", nameof(PaymentDetails.TotalAmount)),
    ];

    private readonly IFeatureFlagService? _featureFlags;

    public GermanyInvoiceStrategy(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.Germany;

    public Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            $"{CountryCode} invoicing behavior is not implemented (AllocateReceiptNumberAsync). See {CountryStrategyDocs.Germany}.");

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
        return UstgDisclosures;
    }

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalKassenSicherheitDe);
        }
    }
}
