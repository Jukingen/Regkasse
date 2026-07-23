using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Providers;

/// <summary>
/// Merges <c>Tse:Providers:fiskaly</c> into legacy <see cref="FiskalyOptions"/> when the dedicated
/// <c>Fiskaly</c> section leaves fields empty — keeps one signing path without duplicating secrets.
/// </summary>
public sealed class FiskalyOptionsFromTseProvidersPostConfigure : IPostConfigureOptions<FiskalyOptions>
{
    private readonly IConfiguration _configuration;

    public FiskalyOptionsFromTseProvidersPostConfigure(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void PostConfigure(string? name, FiskalyOptions options)
    {
        var tse = _configuration.GetSection(TseOptions.SectionName).Get<TseOptions>();
        var vendor = tse?.GetVendorConnection(TseOptions.ProviderFiskaly);
        if (vendor is null)
            return;

        if (string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(vendor.ApiKey))
            options.ApiKey = vendor.ApiKey.Trim();

        if (string.IsNullOrWhiteSpace(options.ApiSecret) && !string.IsNullOrWhiteSpace(vendor.ApiSecret))
            options.ApiSecret = vendor.ApiSecret.Trim();

        if (string.IsNullOrWhiteSpace(options.BaseUrl) && !string.IsNullOrWhiteSpace(vendor.ApiBaseUrl))
            options.BaseUrl = vendor.ApiBaseUrl.Trim();

        if (string.IsNullOrWhiteSpace(options.SignatureCreationUnitId)
            && !string.IsNullOrWhiteSpace(vendor.SignatureCreationUnitId))
            options.SignatureCreationUnitId = vendor.SignatureCreationUnitId.Trim();

        if (string.IsNullOrWhiteSpace(options.SigningCertificateDerBase64)
            && !string.IsNullOrWhiteSpace(vendor.SigningCertificateDerBase64))
            options.SigningCertificateDerBase64 = vendor.SigningCertificateDerBase64.Trim();

        // Prefer enabling Fiskaly when the nested vendor block is usable and production Device mode.
        if (!options.Enabled && vendor.IsUsable)
            options.Enabled = true;
    }
}
