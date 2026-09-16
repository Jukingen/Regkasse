using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Base for a country whose tax module is documented but not implemented. Every member fails loudly
/// and names the document that has to be executed first — never a silent Austrian fallback.
/// </summary>
public abstract class PlannedCountryTaxStrategy : ITaxStrategy
{
    protected PlannedCountryTaxStrategy(string countryCode, string documentationPath)
    {
        CountryCode = countryCode;
        DocumentationPath = documentationPath;
    }

    public string CountryCode { get; }

    /// <summary>Repository-relative doc that specifies the missing behavior.</summary>
    protected string DocumentationPath { get; }

    public TaxCalculationResult CalculateTax(
        IReadOnlyList<TaxLineItemInput> lineItems,
        TaxCalculationContext context) =>
        throw NotImplemented(nameof(CalculateTax));

    public VatIdValidationResult ValidateVatId(string? vatId, CountryProfile profile) =>
        throw NotImplemented(nameof(ValidateVatId));

    public InvoiceFieldRequirements DetermineInvoiceFields(CompanySettings company, Customer? customer) =>
        throw NotImplemented(nameof(DetermineInvoiceFields));

    public RksvTaxSetAmounts? ProjectFiscalTaxSets(string? taxDetailsJson, decimal totalAmount) =>
        throw NotImplemented(nameof(ProjectFiscalTaxSets));

    private NotImplementedException NotImplemented(string member) =>
        new($"{CountryCode} tax behavior is not implemented ({member}). See {DocumentationPath}.");
}

/// <summary>
/// Base for a country whose invoicing module is documented but not implemented. See
/// <see cref="PlannedCountryTaxStrategy"/>.
/// </summary>
public abstract class PlannedCountryInvoiceStrategy : IInvoiceStrategy
{
    protected PlannedCountryInvoiceStrategy(string countryCode, string documentationPath)
    {
        CountryCode = countryCode;
        DocumentationPath = documentationPath;
    }

    public string CountryCode { get; }

    /// <summary>Repository-relative doc that specifies the missing behavior.</summary>
    protected string DocumentationPath { get; }

    public Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default) =>
        throw NotImplemented(nameof(AllocateReceiptNumberAsync));

    public Task<InvoiceDocument> BuildInvoiceDocumentAsync(
        PaymentDetails payment,
        CompanySettings company,
        Customer? customer,
        CancellationToken cancellationToken = default) =>
        throw NotImplemented(nameof(BuildInvoiceDocumentAsync));

    public IReadOnlyList<DisclosureRequirement> GetMandatoryDisclosures(
        CompanySettings company,
        Customer? customer) =>
        throw NotImplemented(nameof(GetMandatoryDisclosures));

    private NotImplementedException NotImplemented(string member) =>
        new($"{CountryCode} invoicing behavior is not implemented ({member}). See {DocumentationPath}.");
}

/// <summary>Documentation paths referenced by the planned-country skeletons.</summary>
public static class CountryStrategyDocs
{
    public const string Germany = "docs/FISCAL_GERMANY.md";
    public const string Switzerland = "docs/FISCAL_SWITZERLAND.md";
    public const string EuDefault = "docs/EINVOICING_EU.md";
}
