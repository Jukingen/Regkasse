using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves the active <see cref="ITseProvider"/> for a named vendor (fiskaly / epson / swissbit / fake).
/// Does not invent a parallel signing stack: fiskaly → RealTseProvider; fake/soft → FakeTseProvider.
/// Epson / Swissbit return stub providers until vendor SDKs are integrated.
/// </summary>
public interface ITseProviderFactory
{
    /// <summary>Active provider name from <c>Tse:Provider</c> (with Mode / Soft / Fiskaly fallbacks).</summary>
    string ResolveConfiguredProviderName();

    /// <summary>Returns the signing backend for <paramref name="providerName"/>.</summary>
    /// <exception cref="ArgumentException">Unknown provider name.</exception>
    ITseProvider GetProvider(string providerName);

    /// <summary>Same as <see cref="GetProvider"/> using <see cref="ResolveConfiguredProviderName"/>.</summary>
    ITseProvider GetConfiguredProvider();

    /// <summary>True when the named vendor has usable credentials (or is fake/soft).</summary>
    bool IsProviderConfigured(string providerName);

    /// <summary>Known vendor keys for admin / diagnostics.</summary>
    IReadOnlyList<string> GetKnownProviderNames();
}
