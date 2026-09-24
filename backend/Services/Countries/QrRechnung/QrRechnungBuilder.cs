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

        var currency = request.Currency.Trim().ToUpperInvariant();
        if (currency is not "CHF" and not "EUR")
        {
            throw new ArgumentException("QR-Rechnung currency must be CHF or EUR.", nameof(request));
        }

        var referenceType = SwissQrEncoder.ResolveReferenceType(request.Reference, request.ReferenceType);
        var reference = string.IsNullOrWhiteSpace(request.Reference)
            ? null
            : request.Reference.Trim().ToUpperInvariant();
        SwissQrEncoder.Validate(iban, referenceType, reference, currency);

        var payload = new QrRechnungPayload(
            Iban: iban,
            Creditor: request.Creditor,
            Debtor: request.Debtor,
            Amount: request.Amount,
            Currency: currency,
            Reference: reference,
            AdditionalInfo: request.AdditionalInfo,
            ReferenceType: referenceType,
            SwissQrText: string.Empty);
        var text = SwissQrEncoder.Encode(payload);
        return Task.FromResult(payload with { SwissQrText = text });
    }

    public async Task<byte[]> BuildPdfAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = await BuildPayloadAsync(request, cancellationToken).ConfigureAwait(false);
        return QrRechnungPdf.Render(payload);
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
