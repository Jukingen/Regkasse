using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Billing;

/// <summary>Outcome of <see cref="ISubscriptionService"/> mutations.</summary>
public sealed class SubscriptionResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public Subscription? Subscription { get; private init; }

    public static SubscriptionResult Success(Subscription subscription) =>
        new()
        {
            Succeeded = true,
            Subscription = subscription
        };

    public static SubscriptionResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };

    /// <summary>Convenience overload matching the product sketch.</summary>
    public static SubscriptionResult Fail(string error) =>
        Fail("SUBSCRIPTION_FAILED", error);
}
