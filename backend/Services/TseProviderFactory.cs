using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Providers;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Multi-vendor TSE factory. fiskaly maps to the existing Real RKSV pipeline;
/// epson/swissbit are explicit stubs until hardware SDKs land.
/// </summary>
public sealed class TseProviderFactory : ITseProviderFactory
{
    private static readonly string[] KnownProviders =
    {
        TseOptions.ProviderFiskaly,
        TseOptions.ProviderEpson,
        TseOptions.ProviderSwissbit,
        TseOptions.ProviderFake,
        TseOptions.ProviderSoft,
    };

    private readonly FakeTseProvider _fake;
    private readonly RealTseProvider _real;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IOptionsMonitor<FiskalyOptions> _fiskalyOptions;
    private readonly ILogger<TseProviderFactory> _logger;

    public TseProviderFactory(
        FakeTseProvider fake,
        RealTseProvider real,
        IOptionsMonitor<TseOptions> tseOptions,
        IOptionsMonitor<FiskalyOptions> fiskalyOptions,
        ILogger<TseProviderFactory> logger)
    {
        _fake = fake;
        _real = real;
        _tseOptions = tseOptions;
        _fiskalyOptions = fiskalyOptions;
        _logger = logger;
    }

    public IReadOnlyList<string> GetKnownProviderNames() => KnownProviders;

    public string ResolveConfiguredProviderName()
    {
        var opts = _tseOptions.CurrentValue;
        if (opts.IsFakeSigningMode)
            return TseOptions.ProviderFake;

        if (opts.UseSoftTseWhenNoDevice && string.IsNullOrWhiteSpace(opts.Provider))
            return TseOptions.ProviderSoft;

        var configured = TseOptions.NormalizeProviderName(opts.Provider);
        if (!string.IsNullOrEmpty(configured))
            return configured;

        if (_fiskalyOptions.CurrentValue.IsConfigured || IsVendorBlockConfigured(TseOptions.ProviderFiskaly))
            return TseOptions.ProviderFiskaly;

        if (opts.UseSoftTseWhenNoDevice)
            return TseOptions.ProviderSoft;

        return TseOptions.ProviderFiskaly;
    }

    public ITseProvider GetConfiguredProvider() => GetProvider(ResolveConfiguredProviderName());

    public bool IsProviderConfigured(string providerName)
    {
        var name = TseOptions.NormalizeProviderName(providerName);
        return name switch
        {
            TseOptions.ProviderFake or TseOptions.ProviderSoft => true,
            TseOptions.ProviderFiskaly => _fiskalyOptions.CurrentValue.IsConfigured
                || IsVendorBlockConfigured(TseOptions.ProviderFiskaly),
            TseOptions.ProviderEpson => IsVendorBlockConfigured(TseOptions.ProviderEpson),
            TseOptions.ProviderSwissbit => IsVendorBlockConfigured(TseOptions.ProviderSwissbit),
            _ => false,
        };
    }

    public ITseProvider GetProvider(string providerName)
    {
        var name = TseOptions.NormalizeProviderName(providerName);
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        switch (name)
        {
            case TseOptions.ProviderFake:
            case TseOptions.ProviderSoft:
                return _fake;

            case TseOptions.ProviderFiskaly:
                if (!IsProviderConfigured(TseOptions.ProviderFiskaly)
                    && !_tseOptions.CurrentValue.IsFakeSigningMode
                    && !_tseOptions.CurrentValue.UseSoftTseWhenNoDevice)
                {
                    _logger.LogWarning(
                        "fiskaly provider selected but credentials are incomplete; RealTseProvider will rely on ITseKeyProvider readiness.");
                }

                return _real;

            case TseOptions.ProviderEpson:
            case TseOptions.ProviderSwissbit:
                _logger.LogWarning(
                    "TSE vendor {Provider} requested but only a stub ITseProvider is registered (not production-ready).",
                    name);
                return new UnsupportedVendorTseProvider(name);

            default:
                throw new ArgumentException(
                    $"Provider '{providerName}' not found. Known: {string.Join(", ", KnownProviders)}",
                    nameof(providerName));
        }
    }

    private bool IsVendorBlockConfigured(string providerName)
    {
        var conn = _tseOptions.CurrentValue.GetVendorConnection(providerName);
        return conn?.IsUsable == true;
    }
}
