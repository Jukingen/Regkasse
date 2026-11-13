namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

/// <summary>SIGN DE finish body shape. HTTP is Paket 76; <see cref="DeReceiptPayload.Raw"/> stays null.</summary>
public sealed record DeReceiptPayload(
    DeStandardV1 StandardV1,
    DeRawProcess? Raw,
    string? Belegnummer);

public sealed record DeStandardV1(DeReceipt Receipt);

public sealed record DeReceipt(
    string ReceiptType,
    IReadOnlyList<DeAmountPerVatRate> AmountsPerVatRate,
    IReadOnlyList<DeAmountPerPaymentType> AmountsPerPaymentType);

/// <summary>Optional SIGN DE <c>raw</c> schema. Not produced in Paket 74.</summary>
public sealed record DeRawProcess(string ProcessType, string ProcessData);

public sealed record DeAmountPerPaymentType(string PaymentType, string Amount, string CurrencyCode);

public static class DeReceiptTypes
{
    public const string Receipt = "RECEIPT";
    public const string Cancellation = "CANCELLATION";
}

public static class DePaymentTypes
{
    public const string Cash = "CASH";
    public const string NonCash = "NON_CASH";
    public const string Eur = "EUR";
}
