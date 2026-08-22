namespace KasseAPI_Final.Tse.Fiskaly;

public sealed record FiskalyAuthResult(bool Success, DateTimeOffset? ExpiresAt, int AccessTokenLength);

public sealed record FiskalyCashRegisterInfo(string Id, string State, bool IsMock = false);

public sealed record FiskalySignedReceipt(
    string Id,
    string CashRegisterId,
    string State,
    string? QrCodeData,
    string? ReceiptNumber,
    string? Environment,
    long? TimeSignature = null,
    bool Signed = false,
    string? CashRegisterSerialNumber = null,
    IReadOnlyList<string>? Hints = null,
    string? ReceiptType = null,
    string? FonValidationsJson = null);

public sealed record FiskalyFonAuthRequest(string FonParticipantId, string FonUserId, string FonUserPin);

public sealed record FiskalyFonAuthResult(
    bool IsAuthenticated,
    string? ParticipantId,
    string? UserId,
    string AuthenticationStatus,
    DateTimeOffset? AuthenticatedAt,
    string? Error = null);

public static class FiskalyResourceStates
{
    public const string Created = "CREATED";
    public const string Pending = "PENDING";
    public const string Registered = "REGISTERED";
    public const string Initialized = "INITIALIZED";
    public const string Authenticated = "AUTHENTICATED";
}

public sealed class FiskalyVatAmount
{
    /// <summary>STANDARD, REDUCED_1, REDUCED_2, SPECIAL, ZERO, or NULL.</summary>
    public string VatRate { get; init; } = "STANDARD";

    public decimal Amount { get; init; }
}

/// <summary>SIGN AT receipt payload (German SIGN DE "transaction" analog).</summary>
public sealed class FiskalyTransactionData
{
    public string CashRegisterId { get; init; } = string.Empty;

    public string ReceiptType { get; init; } = "NORMAL";

    public decimal TotalAmount { get; init; }

    /// <summary>STANDARD, REDUCED_1, REDUCED_2, SPECIAL, ZERO, or NULL.</summary>
    public string VatRate { get; init; } = "STANDARD";

    public string PaymentType { get; init; } = "CASH";

    /// <summary>ISO 4217 currency for <c>amounts_per_payment_type</c>. SIGN AT is EUR.</summary>
    public string CurrencyCode { get; init; } = FiskalyReceiptSchemaMapper.DefaultCurrency;

    /// <summary>
    /// <see cref="FiskalyReceiptSchemaKinds.StandardV1"/> (default) or
    /// <see cref="FiskalyReceiptSchemaKinds.Raw"/>.
    /// </summary>
    public string SchemaKind { get; init; } = FiskalyReceiptSchemaKinds.StandardV1;

    /// <summary>When set, used instead of <see cref="VatRate"/> + <see cref="TotalAmount"/> for mixed VAT.</summary>
    public IReadOnlyList<FiskalyVatAmount>? AmountsPerVatRate { get; init; }

    /// <summary>Optional <c>standard_v1.line_items</c>. A single summary line is synthesized when empty.</summary>
    public IReadOnlyList<FiskalyLineItem>? LineItems { get; init; }
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
