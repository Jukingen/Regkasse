using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Monatsbeleg PDF. Layout, TSE signature, and QR stay in <see cref="SpecialReceiptPdfService"/>.
/// </summary>
public static class MonatsbelegPdfGenerator
{
    public static byte[] Generate(SpecialReceiptPdfData data, byte[]? qrPng = null, string? qrFallbackText = null) =>
        SpecialReceiptPdfService.Generate(data, qrPng, qrFallbackText);
}
