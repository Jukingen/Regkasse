using System.Globalization;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>Catalog of Development-only fiskaly SIGN AT signing scenarios.</summary>
public static class FiskalySignTestScenarios
{
    public static IReadOnlyList<FiskalySignTestScenarioDto> All { get; } =
    [
        new()
        {
            Id = FiskalySignTestScenarioIds.Normal,
            ReceiptType = "NORMAL",
            CanSign = true,
            Description = "Normal receipt: 10.00 EUR at STANDARD VAT.",
            Amounts = [Row("STANDARD", 10.00m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.Cancellation,
            ReceiptType = "CANCELLATION",
            CanSign = true,
            Description = "Cancellation receipt: -10.00 EUR at STANDARD VAT.",
            Amounts = [Row("STANDARD", -10.00m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.Training,
            ReceiptType = "TRAINING",
            CanSign = true,
            Description = "Training receipt: 10.00 EUR (not tax-relevant).",
            Amounts = [Row("STANDARD", 10.00m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.MixedVat,
            ReceiptType = "NORMAL",
            CanSign = true,
            Description = "Mixed VAT: 10.00 STANDARD + 5.50 REDUCED_1.",
            Amounts = [Row("STANDARD", 10.00m), Row("REDUCED_1", 5.50m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.ZeroAmount,
            ReceiptType = "NORMAL",
            CanSign = true,
            Description = "Nullbeleg: 0.00 EUR with VAT rate NULL.",
            Amounts = [Row("NULL", 0.00m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.Raw,
            ReceiptType = "NORMAL",
            CanSign = true,
            Description = "Raw schema fallback: 10.00 EUR STANDARD via gross_amount_standard.",
            Amounts = [Row("STANDARD", 10.00m)]
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.MonthlyClose,
            ReceiptType = "MONTHLY_CLOSE",
            CanSign = false,
            Description = "MONTHLY_CLOSE is created automatically by fiskaly when the month changes; it cannot be signed manually."
        },
        new()
        {
            Id = FiskalySignTestScenarioIds.YearlyClose,
            ReceiptType = "YEARLY_CLOSE",
            CanSign = false,
            Description = "YEARLY_CLOSE is created automatically by fiskaly when the year changes; it cannot be signed manually."
        }
    ];

    public static FiskalySignTestScenarioDto? Find(string? scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
            return All[0];

        var id = scenarioId.Trim().ToLowerInvariant();
        return All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static FiskalyTransactionData ToTransactionData(FiskalySignTestScenarioDto scenario, Guid cashRegisterId)
    {
        var amounts = scenario.Amounts
            .Select(row => new FiskalyVatAmount
            {
                VatRate = row.VatRate,
                Amount = decimal.Parse(row.Amount, CultureInfo.InvariantCulture)
            })
            .ToArray();

        var total = amounts.Sum(a => a.Amount);
        var schemaKind = string.Equals(scenario.Id, FiskalySignTestScenarioIds.Raw, StringComparison.OrdinalIgnoreCase)
            ? FiskalyReceiptSchemaKinds.Raw
            : FiskalyReceiptSchemaKinds.StandardV1;

        return new FiskalyTransactionData
        {
            CashRegisterId = cashRegisterId.ToString("D"),
            ReceiptType = scenario.ReceiptType,
            PaymentType = "CASH",
            CurrencyCode = FiskalyReceiptSchemaMapper.DefaultCurrency,
            SchemaKind = schemaKind,
            TotalAmount = total,
            VatRate = amounts.FirstOrDefault()?.VatRate ?? "STANDARD",
            AmountsPerVatRate = amounts,
            LineItems =
            [
                new FiskalyLineItem
                {
                    Quantity = "1",
                    Text = "Test Produkt",
                    PricePerUnit = FiskalyReceiptSchemaMapper.FormatAmount(total)
                }
            ]
        };
    }

    private static FiskalySignTestVatRowDto Row(string vatRate, decimal amount) =>
        new()
        {
            VatRate = vatRate,
            Amount = amount.ToString("0.00", CultureInfo.InvariantCulture)
        };
}
