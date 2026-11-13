using System.Globalization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

/// <summary>
/// Maps a payment to a SIGN DE <c>standard_v1.receipt</c> payload.
/// VAT buckets come from <see cref="GermanyTaxStrategy.ProjectDeFiscalTaxSets"/>.
/// </summary>
public sealed class DeReceiptPayloadMapper
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly GermanyTaxStrategy _tax;

    public DeReceiptPayloadMapper(GermanyTaxStrategy tax)
    {
        _tax = tax ?? throw new ArgumentNullException(nameof(tax));
    }

    public DeReceiptPayload FromPayment(PaymentDetails payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var taxJson = payment.TaxDetails?.RootElement.GetRawText() ?? "{}";
        var projection = _tax.ProjectDeFiscalTaxSets(taxJson, payment.TotalAmount);
        var sum = projection.AmountsPerVatRate.Sum(row => decimal.Parse(row.Amount, Invariant));
        if (sum != payment.TotalAmount)
        {
            throw new InvalidOperationException(
                $"DE receipt VAT gross {sum.ToString("0.00", Invariant)} does not equal payment total {payment.TotalAmount.ToString("0.00", Invariant)}.");
        }

        var paymentRow = new DeAmountPerPaymentType(
            MapPaymentType(payment.PaymentMethodRaw),
            sum.ToString("0.00", Invariant),
            DePaymentTypes.Eur);

        return new DeReceiptPayload(
            new DeStandardV1(new DeReceipt(
                DeReceiptTypes.Receipt,
                projection.AmountsPerVatRate,
                [paymentRow])),
            Raw: null,
            Belegnummer: payment.ReceiptNumber);
    }

    /// <summary>
    /// Reads <see cref="PaymentDetails.PaymentMethodRaw"/> only.
    /// The not-mapped enum fallback to Cash is not used.
    /// </summary>
    internal static string MapPaymentType(string? paymentMethodRaw)
    {
        return string.Equals(paymentMethodRaw?.Trim(), "0", StringComparison.Ordinal)
            ? DePaymentTypes.Cash
            : DePaymentTypes.NonCash;
    }
}
