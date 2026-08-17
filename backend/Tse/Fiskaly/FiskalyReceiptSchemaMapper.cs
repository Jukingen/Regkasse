using KasseAPI_Final.Models;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>Maps local RKSV tax buckets / payment methods to fiskaly SIGN AT receipt schema.</summary>
public static class FiskalyReceiptSchemaMapper
{
    public const string ReceiptTypeNormal = "NORMAL";
    public const string ReceiptTypeCancellation = "CANCELLATION";

    public static IReadOnlyList<FiskalyVatAmount> FromTaxSets(RksvTaxSetAmounts taxSets)
    {
        ArgumentNullException.ThrowIfNull(taxSets);
        var rows = new List<FiskalyVatAmount>(5);
        AddIfNonZero(rows, "STANDARD", taxSets.Normal);
        AddIfNonZero(rows, "REDUCED_1", taxSets.Ermaessigt1);
        AddIfNonZero(rows, "REDUCED_2", taxSets.Ermaessigt2);
        AddIfNonZero(rows, "ZERO", taxSets.Null);
        AddIfNonZero(rows, "SPECIAL", taxSets.Besonders);

        if (rows.Count == 0)
        {
            rows.Add(new FiskalyVatAmount { VatRate = "NULL", Amount = 0m });
        }

        return rows;
    }

    public static string MapPaymentType(string? paymentMethod)
    {
        var method = (paymentMethod ?? string.Empty).Trim().ToLowerInvariant();
        if (int.TryParse(method, out var numeric)
            && Enum.IsDefined(typeof(PaymentMethod), numeric))
        {
            return ((PaymentMethod)numeric) switch
            {
                PaymentMethod.Cash => "CASH",
                PaymentMethod.Card => "NON_CASH",
                PaymentMethod.Voucher => "VOUCHER",
                _ => "CASH"
            };
        }

        return method switch
        {
            "cash" or "bar" or "barzahlung" => "CASH",
            "card" or "credit" or "debit" or "karte" or "kartenzahlung" => "NON_CASH",
            "voucher" or "gutschein" => "VOUCHER",
            _ => "CASH"
        };
    }

    public static string MapReceiptType(bool isCancellation) =>
        isCancellation ? ReceiptTypeCancellation : ReceiptTypeNormal;

    private static void AddIfNonZero(List<FiskalyVatAmount> rows, string vatRate, decimal amount)
    {
        if (amount == 0m)
            return;
        rows.Add(new FiskalyVatAmount { VatRate = vatRate, Amount = amount });
    }
}
