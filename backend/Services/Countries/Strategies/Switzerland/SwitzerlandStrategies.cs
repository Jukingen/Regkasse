using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies.Switzerland;

/// <summary>
/// Swiss MWST shape: rates from <see cref="ICountryTaxTypeRegistry"/> (8.1 / 2.6 / 3.8), line math
/// from <see cref="CartMoneyHelper.ComputeLine(decimal, int, decimal)"/>, summary from
/// <see cref="CountryRateTaxSummary"/> (not AT <c>TaxTypes</c> buckets).
/// </summary>
public sealed class SwitzerlandTaxStrategy : ITaxStrategy
{
    private readonly ICountryTaxTypeRegistry _taxTypes;
    private readonly IVatIdValidator _vatIdValidator;
    private readonly IFeatureFlagService? _featureFlags;

    public SwitzerlandTaxStrategy()
        : this(new CountryTaxTypeRegistry(), new VatIdValidator(new DisabledViesClient()), featureFlags: null)
    {
    }

    public SwitzerlandTaxStrategy(
        ICountryTaxTypeRegistry taxTypes,
        IVatIdValidator vatIdValidator,
        IFeatureFlagService? featureFlags = null)
    {
        _taxTypes = taxTypes ?? throw new ArgumentNullException(nameof(taxTypes));
        _vatIdValidator = vatIdValidator ?? throw new ArgumentNullException(nameof(vatIdValidator));
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.Switzerland;

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentNullException.ThrowIfNull(context);
        EnsureEnabled();

        var catalog = _taxTypes.Get(CountryProfileCodes.Switzerland);
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in lineItems)
        {
            if (item.VatRatePercent is not decimal percent)
            {
                throw new ArgumentException(
                    "CH line items must use VAT percent from CountryTaxType (8.1, 2.6 or 3.8), not RKSV tax types.",
                    nameof(lineItems));
            }

            var taxType = catalog.FirstOrDefault(t => t.Rate == percent)
                ?? throw new ArgumentException(
                    $"VAT rate {percent} is not a seeded CH CountryTaxType.",
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
            $"{CountryCode} tax behavior is not implemented (ProjectFiscalTaxSets). See {CountryStrategyDocs.Switzerland}.");

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalMwstCh))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalMwstCh);
        }
    }
}

/// <summary>
/// Swiss invoicing shape (MWST disclosures + placeholder document). Numbering and TSE stay
/// unimplemented until call-site wiring (Paket 30-c).
/// </summary>
public sealed class SwitzerlandInvoiceStrategy : IInvoiceStrategy
{
    private static readonly IReadOnlyList<DisclosureRequirement> MwstDisclosures =
    [
        new("seller.vatId", "MWSTG", nameof(CompanySettings.VatId)),
        new("invoice.number", "MWSTG", nameof(PaymentDetails.ReceiptNumber)),
        new("invoice.date", "MWSTG", nameof(PaymentDetails.CreatedAt)),
        new("buyer.name", "MWSTG", nameof(Customer.Name)),
        new("buyer.address", "MWSTG", nameof(Customer.Address)),
        new("invoice.net", "MWSTG", nameof(PaymentDetails.TotalAmount)),
        new("invoice.tax", "MWSTG", nameof(PaymentDetails.TaxAmount)),
        new("invoice.gross", "MWSTG", nameof(PaymentDetails.TotalAmount)),
        new("invoice.mwstBreakdown", "MWSTG", nameof(PaymentDetails.TaxDetails)),
    ];

    private readonly IFeatureFlagService? _featureFlags;

    public SwitzerlandInvoiceStrategy(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public string CountryCode => CountryProfileCodes.Switzerland;

    public Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            $"{CountryCode} invoicing behavior is not implemented (AllocateReceiptNumberAsync). See {CountryStrategyDocs.Switzerland}.");

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
            Currency = "CHF",
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
        return MwstDisclosures;
    }

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalMwstCh))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalMwstCh);
        }
    }
}
