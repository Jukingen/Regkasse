namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>SIX QR-bill reference type (IG 2.3). QRR needs a QR-IBAN; SCOR and NON need an ordinary IBAN.</summary>
public enum QrRechnungReferenceType
{
    Qrr,
    Scor,
    Non,
}

public sealed record QrRechnungParty(
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string CountryCode);

public sealed record QrRechnungRequest(
    string Iban,
    QrRechnungParty Creditor,
    QrRechnungParty? Debtor,
    decimal? Amount,
    string Currency,
    string? Reference,
    string? AdditionalInfo,
    QrRechnungReferenceType? ReferenceType = null);

/// <summary>
/// SIX QR-bill payload. <see cref="SwissQrText"/> is the SPC element list (IG 2.3, address type S).
/// No bank submission.
/// </summary>
public sealed record QrRechnungPayload(
    string Iban,
    QrRechnungParty Creditor,
    QrRechnungParty? Debtor,
    decimal? Amount,
    string Currency,
    string? Reference,
    string? AdditionalInfo,
    QrRechnungReferenceType ReferenceType,
    string SwissQrText);
