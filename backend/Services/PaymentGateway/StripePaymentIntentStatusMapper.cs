using Stripe;

namespace KasseAPI_Final.Services.PaymentGateway;

internal static class StripePaymentIntentStatusMapper
{
    public static PaymentIntentStatus Map(string? stripeStatus) =>
        stripeStatus switch
        {
            "requires_payment_method" => PaymentIntentStatus.Created,
            "requires_confirmation" => PaymentIntentStatus.Pending,
            "requires_action" => PaymentIntentStatus.Pending,
            "processing" => PaymentIntentStatus.Pending,
            "requires_capture" => PaymentIntentStatus.Pending,
            "succeeded" => PaymentIntentStatus.Succeeded,
            "canceled" => PaymentIntentStatus.Cancelled,
            _ => PaymentIntentStatus.Failed
        };

    public static PaymentIntentResult ToResult(PaymentIntent intent)
    {
        var status = Map(intent.Status);
        return new PaymentIntentResult
        {
            Success = status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Created or PaymentIntentStatus.Pending,
            PaymentIntentId = intent.Id,
            ClientSecret = intent.ClientSecret,
            Status = status,
            TransactionId = intent.LatestChargeId,
            CardBrand = intent.PaymentMethod?.Card?.Brand,
            LastFourDigits = intent.PaymentMethod?.Card?.Last4
        };
    }

    internal static long ToStripeAmount(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
