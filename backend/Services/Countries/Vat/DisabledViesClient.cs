namespace KasseAPI_Final.Services.Countries.Vat;

/// <summary>
/// Default VIES client: no network. <see cref="IVatIdValidator"/> must not call this when
/// <c>Vies.CheckEnabled</c> is off. If it is called (flag on, no live client yet), the lookup is
/// reported as unavailable rather than as a false registration match.
/// </summary>
public sealed class DisabledViesClient : IViesClient
{
    public Task<ViesLookupResult> LookupAsync(
        string countryCode,
        string vatNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ViesLookupResult.Unavailable());
    }
}
