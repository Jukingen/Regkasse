using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>Flag-gated EN 16931 stub. Always throws when the flag is on.</summary>
public sealed class NotImplementedEn16931XmlBuilder : IEn16931XmlBuilder
{
    private readonly IFeatureFlagService? _featureFlags;

    public NotImplementedEn16931XmlBuilder(IFeatureFlagService? featureFlags = null)
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
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingEn16931);
        }

        throw new NotImplementedException(
            "EN 16931 XML is not implemented. See docs/EINVOICING_EU.md.");
    }
}
