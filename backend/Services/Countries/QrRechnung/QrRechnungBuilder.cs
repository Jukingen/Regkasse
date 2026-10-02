using System.Globalization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Countries;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Flag-gated SIX QR-bill (IG 2.3). Payload is SPC version 0200, address type S.
/// <see cref="BuildPdfAsync"/> renders the receipt and payment part. No bank HTTP.
/// </summary>
public sealed class QrRechnungBuilder : IQrRechnungBuilder
{
    private readonly IFeatureFlagService? _featureFlags;
    private readonly IAuditLogService? _audit;
    private readonly IActivityEventPublisher? _activity;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IChQrGapAcceptanceService? _gapAcceptance;

    public QrRechnungBuilder(
        IFeatureFlagService? featureFlags = null,
        IAuditLogService? audit = null,
        IActivityEventPublisher? activity = null,
        IHttpContextAccessor? httpContextAccessor = null,
        IChQrGapAcceptanceService? gapAcceptance = null)
    {
        _featureFlags = featureFlags;
        _audit = audit;
        _activity = activity;
        _httpContextAccessor = httpContextAccessor;
        _gapAcceptance = gapAcceptance;
    }

    public async Task<QrRechnungPayload> BuildPayloadAsync(
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
        var built = payload with { SwissQrText = text };
        await QrRechnungAudit.PayloadBuiltAsync(
            _audit,
            _activity,
            _httpContextAccessor,
            request,
            built,
            request.ActorUserId,
            cancellationToken).ConfigureAwait(false);
        if (_gapAcceptance is not null && request.TenantId is Guid tenantId && tenantId != Guid.Empty)
        {
            await _gapAcceptance.WarnOutstandingAsync(tenantId, request.InvoiceId, cancellationToken)
                .ConfigureAwait(false);
        }

        return built;
    }

    public async Task<byte[]> BuildPdfAsync(
        QrRechnungRequest request,
        CancellationToken cancellationToken = default)
    {
        QrRechnungAudit.EnsureRelativePdfPath(request.PdfPathRelative);
        var payload = await BuildPayloadAsync(request, cancellationToken).ConfigureAwait(false);
        var pdf = QrRechnungPdf.Render(payload);
        await QrRechnungAudit.PdfGeneratedAsync(
            _audit,
            _activity,
            _httpContextAccessor,
            request,
            payload,
            cancellationToken).ConfigureAwait(false);
        return pdf;
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
