using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>Flag-gated XRechnung stub. Always throws when the flag is on.</summary>
public sealed class NotImplementedXrechnungXmlBuilder : IXrechnungXmlBuilder
{
    private readonly IFeatureFlagService? _featureFlags;

    public NotImplementedXrechnungXmlBuilder(IFeatureFlagService? featureFlags = null)
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
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingXRechnung))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingXRechnung);
        }

        throw new NotImplementedException(
            "XRechnung XML is not implemented. See docs/FISCAL_GERMANY.md.");
    }
}
