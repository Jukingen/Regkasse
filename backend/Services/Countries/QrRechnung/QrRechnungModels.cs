namespace KasseAPI_Final.Services.Countries.QrRechnung;

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
    string? AdditionalInfo);

/// <summary>SIX QR-bill payload shape. No bank submission and no QR image.</summary>
public sealed record QrRechnungPayload(
    string Iban,
    QrRechnungParty Creditor,
    QrRechnungParty? Debtor,
    decimal? Amount,
    string Currency,
    string? Reference,
    string? AdditionalInfo);
