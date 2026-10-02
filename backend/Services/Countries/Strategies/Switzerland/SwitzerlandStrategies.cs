using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries.Strategies.Switzerland;

/// <summary>
/// Swiss MWST shape: percents come from the CH catalog (<see cref="ICountryTaxTypeRegistry"/>,
/// then <see cref="IChMwstEffectiveRates"/> when the calculation names a tenant). Line math stays in
/// <see cref="CartMoneyHelper.ComputeLine(decimal, int, decimal)"/>. This type does not store a rate.
/// </summary>
public sealed class SwitzerlandTaxStrategy : ITaxStrategy
{
    private readonly ICountryTaxTypeRegistry _taxTypes;
    private readonly IVatIdValidator _vatIdValidator;
    private readonly IFeatureFlagService? _featureFlags;
    private readonly IChMwstEffectiveRates? _effectiveRates;

    public SwitzerlandTaxStrategy()
        : this(new CountryTaxTypeRegistry(), new VatIdValidator(new DisabledViesClient()), featureFlags: null)
    {
    }

    public SwitzerlandTaxStrategy(
        ICountryTaxTypeRegistry taxTypes,
        IVatIdValidator vatIdValidator,
        IFeatureFlagService? featureFlags = null,
        IChMwstEffectiveRates? effectiveRates = null)
    {
        _taxTypes = taxTypes ?? throw new ArgumentNullException(nameof(taxTypes));
        _vatIdValidator = vatIdValidator ?? throw new ArgumentNullException(nameof(vatIdValidator));
        _featureFlags = featureFlags;
        _effectiveRates = effectiveRates;
    }

    public string CountryCode => CountryProfileCodes.Switzerland;

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentNullException.ThrowIfNull(context);
        EnsureEnabled();

        if (context.VatRegime is VatRegime.EU_REVERSE_CHARGE or VatRegime.EU_OSS)
        {
            throw new ArgumentException(
                "CH MWST does not apply reverse charge or OSS. Switzerland is outside the EU VAT area.",
                nameof(context));
        }

        if (context.TaxExempt || context.VatRegime == VatRegime.CH_KLEINUNTERNEHMER)
            return CalculateKleinunternehmer(lineItems);

        var catalog = CatalogFor(context);
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        var taxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in lineItems)
        {
            if (item.VatRatePercent is not decimal percent)
            {
                throw new ArgumentException(
                    "CH line items must use a VAT percent from the effective CH CountryTaxType catalog, not RKSV tax types.",
                    nameof(lineItems));
            }

            var taxType = catalog.FirstOrDefault(t => t.Rate == percent)
                ?? throw new ArgumentException(
                    $"VAT rate {percent} is not an effective CH CountryTaxType.",
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

    /// <summary>
    /// MWST small-business exemption: gross stays gross, tax is zero. Buyer VAT-ID is ignored
    /// (no reverse charge, no VIES).
    /// </summary>
    private static TaxCalculationResult CalculateKleinunternehmer(IReadOnlyList<TaxLineItemInput> lineItems)
    {
        var lines = new List<CartMoneyHelper.LineAmounts>(lineItems.Count);
        decimal tax = 0m;
        foreach (var item in lineItems)
        {
            var line = CartMoneyHelper.ComputeLine(item.UnitPriceGross, item.Quantity, 0m);
            lines.Add(line);
            tax += line.LineTax;
        }

        return new TaxCalculationResult
        {
            Lines = lines,
            TaxSummary = CountryRateTaxSummary.FromLineRates(lines),
            Totals = CountryRateTaxSummary.Totals(lines),
            TaxDetails = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [CountryTaxTypeCodes.Zero] = tax,
            },
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
        if (company.VatRegime is VatRegime.EU_REVERSE_CHARGE or VatRegime.EU_OSS)
        {
            throw new ArgumentException(
                "CH MWST does not apply reverse charge or OSS.",
                nameof(company));
        }

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

    public RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount)
    {
        EnsureEnabled();
        var catalog = _taxTypes.Get(CountryProfileCodes.Switzerland);
        return ChTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, totalAmount, catalog);
    }

    private IReadOnlyList<CountryTaxType> CatalogFor(TaxCalculationContext context)
    {
        if (_effectiveRates is not null && context.TenantId is Guid tenantId && tenantId != Guid.Empty)
            return _effectiveRates.Resolve(tenantId, applyOverride: true).Rates;

        return _taxTypes.Get(CountryProfileCodes.Switzerland);
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

/// <summary>
/// Swiss invoicing shape (MWST disclosures). The QR-Rechnung payload comes only from
/// <see cref="IQrRechnungBuilder"/>. Receipt numbers come from
/// <see cref="ChReceiptSequenceService"/> (<c>ch_receipt_sequences</c>), not the Austrian sequence table.
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
        new("invoice.kleinunternehmer", "MWSTG", nameof(CompanySettings.TaxExempt)),
    ];

    private readonly IFeatureFlagService? _featureFlags;
    private readonly IQrRechnungBuilder _qr;
    private readonly IChReceiptSequenceService? _sequences;
    private readonly AppDbContext? _db;

    public SwitzerlandInvoiceStrategy(
        IFeatureFlagService? featureFlags = null,
        IQrRechnungBuilder? qr = null,
        IChReceiptSequenceService? sequences = null,
        AppDbContext? db = null)
    {
        _featureFlags = featureFlags;
        _qr = qr ?? new QrRechnungBuilder();
        _sequences = sequences;
        _db = db;
    }

    public string CountryCode => CountryProfileCodes.Switzerland;

    public async Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureEnabled();
        if (_sequences is null || _db is null)
            throw new InvalidOperationException("CH receipt sequence service is not configured.");

        var register = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.Id == context.CashRegisterId && r.IsActive)
            .Select(r => new { r.TenantId, r.RegisterNumber })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (register is null || string.IsNullOrWhiteSpace(register.RegisterNumber))
            throw new KeyNotFoundException($"Active cash register '{context.CashRegisterId}' was not found.");

        var slug = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == register.TenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException($"Tenant slug missing for '{register.TenantId}'.");

        var attempts = Math.Clamp(context.MaxAttempts, 1, 10);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var sequence = await _sequences.AllocateNextAsync(register.TenantId, context.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            var number = ChReceiptSequenceService.FormatChBelegNr(slug, register.RegisterNumber, sequence);
            var taken = await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == context.CashRegisterId && p.IsActive && p.ReceiptNumber == number,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!taken)
                return number;
        }

        throw new InvalidOperationException(
            $"Could not reserve a CH receipt number for cash register '{context.CashRegisterId}' after {attempts} attempt(s).");
    }

    public async Task<InvoiceDocument> BuildInvoiceDocumentAsync(
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
        var currency = string.IsNullOrWhiteSpace(company.Currency) ? "CHF" : company.Currency.Trim();
        var qr = await _qr.BuildPayloadAsync(
            new QrRechnungRequest(
                Iban: company.BankAccountNumber ?? string.Empty,
                Creditor: new QrRechnungParty(
                    company.CompanyName,
                    company.CompanyAddress,
                    null,
                    string.Empty,
                    string.Empty,
                    CountryProfileCodes.Switzerland),
                Debtor: null,
                Amount: payment.TotalAmount,
                Currency: currency,
                Reference: null,
                AdditionalInfo: payment.ReceiptNumber,
                ReferenceType: QrRechnungReferenceType.Non,
                InvoiceId: payment.Id,
                TenantId: company.TenantId,
                ActorUserId: payment.CashierId),
            cancellationToken).ConfigureAwait(false);

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
            Currency = currency,
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

        return new InvoiceDocument(CountryCode, receipt, structured, qr);
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
