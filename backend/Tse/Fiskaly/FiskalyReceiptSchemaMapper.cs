using System.Globalization;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Tse.Fiskaly;

public static class FiskalyReceiptSchemaKinds
{
    public const string StandardV1 = "standard_v1";
    public const string Raw = "raw";
}

/// <summary>Maps local RKSV tax buckets / payment methods to fiskaly SIGN AT receipt schema.</summary>
public static class FiskalyReceiptSchemaMapper
{
    public const string ReceiptTypeNormal = "NORMAL";
    public const string ReceiptTypeCancellation = "CANCELLATION";
    public const string DefaultCurrency = "EUR";
    public const string DefaultLineItemText = "Verkauf";

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

    public static bool IsRawSchema(string? schemaKind) =>
        string.Equals(schemaKind?.Trim(), FiskalyReceiptSchemaKinds.Raw, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<FiskalyVatAmount> ResolveVatRows(FiskalyTransactionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.AmountsPerVatRate is { Count: > 0 })
        {
            return data.AmountsPerVatRate
                .Select(row => new FiskalyVatAmount
                {
                    VatRate = string.IsNullOrWhiteSpace(row.VatRate)
                        ? "STANDARD"
                        : row.VatRate.Trim().ToUpperInvariant(),
                    Amount = row.Amount
                })
                .ToArray();
        }

        return
        [
            new FiskalyVatAmount
            {
                VatRate = string.IsNullOrWhiteSpace(data.VatRate)
                    ? "STANDARD"
                    : data.VatRate.Trim().ToUpperInvariant(),
                Amount = data.TotalAmount
            }
        ];
    }

    /// <summary>
    /// SIGN AT PUT /receipt body. Uses <c>standard_v1</c> (no nested <c>receipt</c>) or <c>raw</c>.
    /// </summary>
    public static FiskalySignReceiptRequest BuildSignRequest(FiskalyTransactionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var vatRows = ResolveVatRows(data);
        var receiptType = string.IsNullOrWhiteSpace(data.ReceiptType)
            ? ReceiptTypeNormal
            : data.ReceiptType.Trim().ToUpperInvariant();

        return new FiskalySignReceiptRequest
        {
            ReceiptType = receiptType,
            Schema = IsRawSchema(data.SchemaKind)
                ? new FiskalyReceiptSchema { Raw = BuildRawSchema(vatRows) }
                : new FiskalyReceiptSchema { StandardV1 = BuildStandardV1(data, vatRows) }
        };
    }

    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static FiskalyStandardV1Schema BuildStandardV1(
        FiskalyTransactionData data,
        IReadOnlyList<FiskalyVatAmount> vatRows)
    {
        var paymentType = string.IsNullOrWhiteSpace(data.PaymentType)
            ? "CASH"
            : data.PaymentType.Trim().ToUpperInvariant();
        var currency = string.IsNullOrWhiteSpace(data.CurrencyCode)
            ? DefaultCurrency
            : data.CurrencyCode.Trim().ToUpperInvariant();
        var paymentAmount = FormatAmount(vatRows.Sum(row => row.Amount));

        return new FiskalyStandardV1Schema
        {
            AmountsPerVatRate = vatRows
                .Select(row => new FiskalyVatRateAmountDto
                {
                    VatRate = row.VatRate,
                    Amount = FormatAmount(row.Amount)
                })
                .ToArray(),
            LineItems = ResolveLineItems(data, vatRows),
            AmountsPerPaymentType =
            [
                new FiskalyPaymentTypeAmountDto
                {
                    PaymentType = paymentType,
                    Amount = paymentAmount,
                    CurrencyCode = currency
                }
            ]
        };
    }

    private static FiskalyRawSchema BuildRawSchema(IReadOnlyList<FiskalyVatAmount> vatRows)
    {
        decimal AmountOf(string vatRate) =>
            vatRows
                .Where(row => string.Equals(row.VatRate, vatRate, StringComparison.OrdinalIgnoreCase))
                .Sum(row => row.Amount);

        return new FiskalyRawSchema
        {
            GrossAmountStandard = FormatAmount(AmountOf("STANDARD")),
            GrossAmountReduced1 = FormatAmount(AmountOf("REDUCED_1")),
            GrossAmountReduced2 = FormatAmount(AmountOf("REDUCED_2")),
            GrossAmountSpecial = FormatAmount(AmountOf("SPECIAL")),
            GrossAmountZero = FormatAmount(AmountOf("ZERO") + AmountOf("NULL"))
        };
    }

    private static IReadOnlyList<FiskalyLineItemDto> ResolveLineItems(
        FiskalyTransactionData data,
        IReadOnlyList<FiskalyVatAmount> vatRows)
    {
        if (data.LineItems is { Count: > 0 })
        {
            return data.LineItems
                .Select(item => new FiskalyLineItemDto
                {
                    Quantity = string.IsNullOrWhiteSpace(item.Quantity) ? "1" : item.Quantity.Trim(),
                    Text = string.IsNullOrWhiteSpace(item.Text) ? DefaultLineItemText : item.Text.Trim(),
                    PricePerUnit = string.IsNullOrWhiteSpace(item.PricePerUnit)
                        ? FormatAmount(0m)
                        : item.PricePerUnit.Trim()
                })
                .ToArray();
        }

        return
        [
            new FiskalyLineItemDto
            {
                Quantity = "1",
                Text = DefaultLineItemText,
                PricePerUnit = FormatAmount(vatRows.Sum(row => row.Amount))
            }
        ];
    }

    private static void AddIfNonZero(List<FiskalyVatAmount> rows, string vatRate, decimal amount)
    {
        if (amount == 0m)
            return;
        rows.Add(new FiskalyVatAmount { VatRate = vatRate, Amount = amount });
    }
}

public sealed class FiskalyLineItem
{
    public string Quantity { get; init; } = "1";

    public string Text { get; init; } = FiskalyReceiptSchemaMapper.DefaultLineItemText;

    public string PricePerUnit { get; init; } = "0.00";
}

public sealed class FiskalySignReceiptRequest
{
    [JsonPropertyName("receipt_type")]
    public string ReceiptType { get; init; } = FiskalyReceiptSchemaMapper.ReceiptTypeNormal;

    [JsonPropertyName("schema")]
    public FiskalyReceiptSchema Schema { get; init; } = new();
}

public sealed class FiskalyReceiptSchema
{
    [JsonPropertyName("standard_v1")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FiskalyStandardV1Schema? StandardV1 { get; init; }

    [JsonPropertyName("raw")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FiskalyRawSchema? Raw { get; init; }
}

public sealed class FiskalyStandardV1Schema
{
    [JsonPropertyName("amounts_per_vat_rate")]
    public IReadOnlyList<FiskalyVatRateAmountDto> AmountsPerVatRate { get; init; } =
        Array.Empty<FiskalyVatRateAmountDto>();

    [JsonPropertyName("line_items")]
    public IReadOnlyList<FiskalyLineItemDto> LineItems { get; init; } =
        Array.Empty<FiskalyLineItemDto>();

    [JsonPropertyName("amounts_per_payment_type")]
    public IReadOnlyList<FiskalyPaymentTypeAmountDto> AmountsPerPaymentType { get; init; } =
        Array.Empty<FiskalyPaymentTypeAmountDto>();
}

public sealed class FiskalyVatRateAmountDto
{
    [JsonPropertyName("vat_rate")]
    public string VatRate { get; init; } = "STANDARD";

    [JsonPropertyName("amount")]
    public string Amount { get; init; } = "0.00";
}

public sealed class FiskalyPaymentTypeAmountDto
{
    [JsonPropertyName("payment_type")]
    public string PaymentType { get; init; } = "CASH";

    [JsonPropertyName("amount")]
    public string Amount { get; init; } = "0.00";

    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; init; } = FiskalyReceiptSchemaMapper.DefaultCurrency;
}

public sealed class FiskalyLineItemDto
{
    [JsonPropertyName("quantity")]
    public string Quantity { get; init; } = "1";

    [JsonPropertyName("text")]
    public string Text { get; init; } = FiskalyReceiptSchemaMapper.DefaultLineItemText;

    [JsonPropertyName("price_per_unit")]
    public string PricePerUnit { get; init; } = "0.00";
}

public sealed class FiskalyRawSchema
{
    [JsonPropertyName("gross_amount_standard")]
    public string GrossAmountStandard { get; init; } = "0.00";

    [JsonPropertyName("gross_amount_reduced_1")]
    public string GrossAmountReduced1 { get; init; } = "0.00";

    [JsonPropertyName("gross_amount_reduced_2")]
    public string GrossAmountReduced2 { get; init; } = "0.00";

    [JsonPropertyName("gross_amount_special")]
    public string GrossAmountSpecial { get; init; } = "0.00";

    [JsonPropertyName("gross_amount_zero")]
    public string GrossAmountZero { get; init; } = "0.00";
}
