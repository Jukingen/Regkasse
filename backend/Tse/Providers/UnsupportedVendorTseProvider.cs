namespace KasseAPI_Final.Tse.Providers;

/// <summary>
/// Stub <see cref="ITseProvider"/> for vendors that are configured in options but not yet integrated
/// (Epson / Swissbit). Ready = false; Sign throws a clear operator-facing error.
/// </summary>
public sealed class UnsupportedVendorTseProvider : ITseProvider
{
    private readonly string _vendorName;

    public UnsupportedVendorTseProvider(string vendorName)
    {
        _vendorName = string.IsNullOrWhiteSpace(vendorName) ? "unknown" : vendorName.Trim();
    }

    public string VendorName => _vendorName;

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<TseSignResult> SignAsync(
        BelegdatenPayload payload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            $"TSE vendor '{_vendorName}' is not implemented. Use Provider=fiskaly (production) or Mode=Fake / TseMode=Demo for Soft TSE.");
    }
}
