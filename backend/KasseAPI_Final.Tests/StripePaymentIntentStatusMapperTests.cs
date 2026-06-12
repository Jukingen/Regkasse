using KasseAPI_Final.Services.PaymentGateway;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class StripePaymentIntentStatusMapperTests
{
    [Theory]
    [InlineData("requires_payment_method", PaymentIntentStatus.Created)]
    [InlineData("requires_confirmation", PaymentIntentStatus.Pending)]
    [InlineData("requires_action", PaymentIntentStatus.Pending)]
    [InlineData("processing", PaymentIntentStatus.Pending)]
    [InlineData("succeeded", PaymentIntentStatus.Succeeded)]
    [InlineData("canceled", PaymentIntentStatus.Cancelled)]
    [InlineData("unknown", PaymentIntentStatus.Failed)]
    public void Map_translatesStripeStatuses(string stripeStatus, PaymentIntentStatus expected) =>
        Assert.Equal(expected, StripePaymentIntentStatusMapper.Map(stripeStatus));

    [Theory]
    [InlineData(10.00, 1000)]
    [InlineData(12.34, 1234)]
    [InlineData(0.01, 1)]
    public void ToStripeAmount_convertsToMinorUnits(decimal amount, long expected) =>
        Assert.Equal(expected, StripePaymentIntentStatusMapper.ToStripeAmount(amount));
}
