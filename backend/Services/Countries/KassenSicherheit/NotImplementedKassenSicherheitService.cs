using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// Flag-gated stub. <c>not-configured</c> is a no-op; any other provider throws
/// <see cref="NotImplementedException"/>. Fake providers are rejected at startup (Paket 17).
/// </summary>
public sealed class NotImplementedKassenSicherheitService : IKassenSicherheitService
{
    private readonly IFeatureFlagService _featureFlags;
    private readonly IOptions<KassenSicherheitOptions> _options;

    public NotImplementedKassenSicherheitService(
        IFeatureFlagService featureFlags,
        IOptions<KassenSicherheitOptions> options)
    {
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe))
            throw new FeatureDisabledException(FeatureFlagNames.FiscalKassenSicherheitDe);

        var provider = _options.Value.Provider?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, CountryFiscalLockEvaluator.SentinelNotConfigured, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new KassenSicherheitSignResult(
                Signed: false,
                Signature: null,
                Provider: string.IsNullOrWhiteSpace(provider)
                    ? CountryFiscalLockEvaluator.SentinelNotConfigured
                    : provider));
        }

        throw new NotImplementedException(
            "DE KassenSicherheit provider is not implemented. See docs/FISCAL_GERMANY.md.");
    }
}
