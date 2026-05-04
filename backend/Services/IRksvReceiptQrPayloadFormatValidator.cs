using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Validates internal RKSV receipt QR wire format (<c>_R1-AT1_...</c>) without cryptographic verification or DB access.
/// </summary>
public interface IRksvReceiptQrPayloadFormatValidator
{
    RksvValidateReceiptQrResponse Validate(string? qrPayload);
}
