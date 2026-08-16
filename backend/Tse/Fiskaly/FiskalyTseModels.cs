namespace KasseAPI_Final.Tse.Fiskaly;

public sealed record FiskalyAuthResult(bool Success, DateTimeOffset? ExpiresAt, int AccessTokenLength);

public sealed record FiskalyCashRegisterInfo(string Id, string State);

public sealed record FiskalySignedReceipt(
    string Id,
    string CashRegisterId,
    string State,
    string? QrCodeData,
    string? ReceiptNumber,
    string? Environment);

/// <summary>SIGN AT receipt payload (German SIGN DE "transaction" analog).</summary>
public sealed class FiskalyTransactionData
{
    public string CashRegisterId { get; init; } = string.Empty;

    public string ReceiptType { get; init; } = "NORMAL";

    public decimal TotalAmount { get; init; }

    /// <summary>STANDARD, REDUCED, SPECIAL, ZERO, or NULL.</summary>
    public string VatRate { get; init; } = "STANDARD";

    public string PaymentType { get; init; } = "CASH";
}

public sealed class FiskalyResourceEnsureResult
{
    public bool Success { get; init; }

    public string? ScuId { get; init; }

    public string? ScuState { get; init; }

    public string? CashRegisterId { get; init; }

    public string? CashRegisterState { get; init; }

    public string Message { get; init; } = string.Empty;
}
