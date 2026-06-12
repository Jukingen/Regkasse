namespace KasseAPI_Final.DTOs;

/// <summary>
/// RKSV §8 company header for POS receipts (read-only). Source: <see cref="Models.CompanySettings"/>.
/// </summary>
public sealed class PosCompanyInfoDto
{
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyAddress { get; init; } = string.Empty;
    public string TaxNumber { get; init; } = string.Empty;
    public string? ReceiptFooter { get; init; }
}
