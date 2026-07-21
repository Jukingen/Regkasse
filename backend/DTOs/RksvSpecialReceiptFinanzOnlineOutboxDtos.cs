namespace KasseAPI_Final.DTOs;

/// <summary>Serialized into <see cref="FinanzOnlineIntegration.FinanzOnlineOutboxPayload.PayloadJson"/> for RKSV Startbeleg/Jahresbeleg FO outbox jobs.</summary>
public sealed class RksvSpecialReceiptFinanzOnlineOutboxPayloadBody
{
    public string Kind { get; set; } = string.Empty;

    public Guid PaymentId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid CashRegisterId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>RKSV QR payload from persisted receipt (same as <c>receipts.qr_code_payload</c>).</summary>
    public string QrPayload { get; set; } = string.Empty;
}
