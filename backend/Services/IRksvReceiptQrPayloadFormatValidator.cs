using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Validates RKSV receipt QR wire format (BMF §9 standard or legacy internal compact) without cryptographic verification or DB access.
/// </summary>
public interface IRksvReceiptQrPayloadFormatValidator
{
    RksvValidateReceiptQrResponse Validate(string? qrPayload);
}
