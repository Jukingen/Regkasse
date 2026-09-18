using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>ZUGFeRD XML builder. Syntax generation is not implemented.</summary>
public interface IZugferdXmlBuilder
{
    Task<string> BuildXmlAsync(InvoiceDocumentDto document, CancellationToken cancellationToken = default);
}

/// <summary>Flag-gated ZUGFeRD stub. Always throws when the flag is on.</summary>
public sealed class NotImplementedZugferdXmlBuilder : IZugferdXmlBuilder
{
    private readonly IFeatureFlagService? _featureFlags;

    public NotImplementedZugferdXmlBuilder(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public Task<string> BuildXmlAsync(
        InvoiceDocumentDto document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingZugferd))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingZugferd);
        }

        throw new NotImplementedException(
            "ZUGFeRD XML is not implemented. See docs/FISCAL_GERMANY.md.");
    }
}
