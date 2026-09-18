using System.Globalization;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Countries;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Flag-gated SIX QR-bill payload. IBAN must be CH or LI. PDF/QR image generation is not implemented.
/// </summary>
public sealed class QrRechnungBuilder : IQrRechnungBuilder
{
    private readonly IFeatureFlagService? _featureFlags;

    public QrRechnungBuilder(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public Task<QrRechnungPayload> BuildPayloadAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Creditor);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureEnabled();

        var iban = NormalizeSwissOrLiechtensteinIban(request.Iban);
        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("QR-Rechnung currency is required.", nameof(request));
        }

        return Task.FromResult(new QrRechnungPayload(
            Iban: iban,
            Creditor: request.Creditor,
            Debtor: request.Debtor,
            Amount: request.Amount,
            Currency: request.Currency.Trim().ToUpperInvariant(),
            Reference: request.Reference,
            AdditionalInfo: request.AdditionalInfo));
    }

    public Task<byte[]> BuildPdfAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureEnabled();

        throw new NotImplementedException(
            "QR-Rechnung PDF/QR image generation is not implemented. See docs/FISCAL_SWITZERLAND.md.");
    }

    private void EnsureEnabled()
    {
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingQrRechnung))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingQrRechnung);
        }
    }

    /// <summary>Strip whitespace, uppercase, require CH or LI prefix. No IBAN checksum.</summary>
    internal static string NormalizeSwissOrLiechtensteinIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            throw new ArgumentException(
                "QR-Rechnung IBAN is required and must start with CH or LI.",
                nameof(iban));
        }

        var buffer = new char[iban.Length];
        var written = 0;
        foreach (var ch in iban)
        {
            if (char.IsWhiteSpace(ch))
                continue;
            buffer[written++] = char.ToUpper(ch, CultureInfo.InvariantCulture);
        }

        var normalized = new string(buffer, 0, written);
        var prefix = normalized.Length >= 2 ? normalized[..2] : string.Empty;
        if (prefix is not "CH" and not "LI")
        {
            throw new ArgumentException(
                "QR-Rechnung IBAN must start with CH or LI.",
                nameof(iban));
        }

        return normalized;
    }
}
